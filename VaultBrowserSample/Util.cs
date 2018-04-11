using System;
using System.Collections.Generic;
using Autodesk.Forge;
using System.Linq;
using System.Windows;
using System.Text;
using System.Threading.Tasks;
using VDF = Autodesk.DataManagement.Client.Framework;
using System.Windows.Forms;
using System.IO.Compression;
using System.IO;
using System.Configuration;
using Autodesk.Forge.Model;
using System.Text.RegularExpressions;

namespace VaultBrowserSample
{
    class Util
    {
        private static List<string> fileList { get; set; }
        private static dynamic InternalToken { get; set; }
        private static dynamic PublicToken { get; set; }
        private static string dwnldfldrpath { get; set; }
        private static string upldfldrpath { get; set; }
        private static string zippedfldrpath { get; set; }
        private static string zipfile { get; set; }


        /// <summary>
        /// Create required folders in the user temp folder.
        /// </summary>
        private static string createDirectories()
        {
            // Need to create this directory or change this code to an available directory. 
            // Where you want the vault files to be downloaded to. Currently set to user's temp path.
            string temppath = Path.GetTempPath();
            dwnldfldrpath = temppath + "Vault_Forge_Downloaded_Files";
            upldfldrpath = temppath + "Vault_Forge_Files_To_Upload";
            zippedfldrpath = temppath + "Vault_Forge_zip";
            zipfile = zippedfldrpath + @"\Files_ToUpload.zip";
            try
            {
                Directory.CreateDirectory(dwnldfldrpath);
                Directory.CreateDirectory(upldfldrpath);
                Directory.CreateDirectory(zippedfldrpath);
                return ("Created!");
            }
            catch (Exception ex)
            {
                Console.WriteLine("createDirectories failed:" + ex.Message);
                return ("Error");
            }
        }

        /// <summary>
        /// This method is called from Program.cs. Entry point to Vault code.
        /// </summary>
        public static string DownloadFilestoFolder(VDF.Vault.Currency.Entities.FileIteration fileIter, VDF.Vault.Currency.Connections.Connection connection)
        {
            try
            {
                string result = createDirectories();
                try
                {
                    string nameOfTopLevelAsm = fileIter.EntityName;
                    downloadFile(connection, fileIter, dwnldfldrpath, nameOfTopLevelAsm);
                    return "Downloaded Successfully!";
                }
                catch (Exception ex)
                {
                    Console.WriteLine("downloadFile failed:" + ex.Message);
                    return "Directory not found";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("createDirectories failed:" + ex.Message);
                return "Directory not found";
            }
        }

        /// <summary>
        /// Download the file(s) to the local folder.
        /// </summary>
        private static void downloadFile(VDF.Vault.Currency.Connections.Connection connection, VDF.Vault.Currency.Entities.FileIteration fileIter, string folderPath, string topAssemblyName)
        {
            VDF.Vault.Settings.AcquireFilesSettings settings =
                    new VDF.Vault.Settings.AcquireFilesSettings(connection);
            settings.AddEntityToAcquire(fileIter);
            settings.OptionsRelationshipGathering.FileRelationshipSettings.IncludeChildren = true;
            settings.OptionsRelationshipGathering.FileRelationshipSettings.RecurseChildren = true;
            settings.LocalPath = new VDF.Currency.FolderPathAbsolute(folderPath);
            settings.DefaultAcquisitionOption =
                VDF.Vault.Settings.AcquireFilesSettings.AcquisitionOption.Download;

            VDF.Vault.Results.AcquireFilesResults myAcqFilesResults =
                                    connection.FileManager.AcquireFiles(settings);

            fileList = new List<string>();
            foreach (VDF.Vault.Results.FileAcquisitionResult myFileAcquistionResult in myAcqFilesResults.FileResults)
            {
                if (myFileAcquistionResult.File.EntityName == topAssemblyName)
                {
                    fileList.Insert(0, myFileAcquistionResult.LocalPath.FullPath);
                    continue;
                }
                fileList.Add(myFileAcquistionResult.LocalPath.FullPath);
            }           
        }

        /// <summary>
        /// This method is called from Program.cs, after the Vault code has executed successfully.
        /// </summary>
        public async static Task UploadAssembly()
        {
            try
            {
                Console.WriteLine("Creating Zip file...");
                CreateZipFile();
                try
                {
                    Console.WriteLine("Fetching internal token...");
                    InternalToken = await GetInternalAsync();
                    try
                    {
                        Console.WriteLine("Creating bucket...");
                        dynamic bucket = await CreateBucket();
                        try
                        {
                            Console.WriteLine("Uploading Zip file...");
                            dynamic uploadedobject = await UploadZipFile(bucket.bucketKey);
                            try
                            {
                                Console.WriteLine("Translating Zip file...");
                                dynamic translatedobject = await TranslateZipFile(uploadedobject);
                                try
                                {
                                    Console.WriteLine("Fetching Public token...");
                                    PublicToken = await GetPublicAsync();
                                }
                                catch (Exception ex) { Console.WriteLine("GetPublicAsync failed:" +ex.Message); }
                                Console.WriteLine("Opening document in browser...");
                                openViewer(translatedobject.urn);
                            }                            
                            catch(Exception ex) { Console.WriteLine("UploadZipFile failed: " + ex.Message); }
                        }
                        catch(Exception ex) { Console.WriteLine("UploadZipFile failed: " + ex.Message); }
                    }
                    catch(Exception ex) { Console.WriteLine("CreateBucket failed: " + ex.Message); }
                }
                catch (Exception ex) { Console.WriteLine("GetInternalAsync failed: " + ex.Message); }
            }
            catch (Exception ex) { Console.WriteLine("CreateZipFile failed: " + ex.Message); }            
        }


        /// <summary>
        /// Create the zip file.
        /// </summary>
        private static void CreateZipFile()
        {
            string fileName = string.Empty;
            string filePathName = string.Empty;

            foreach (string file in fileList)
            {
                // AcquireFiles downloads files we do not want in the zip (.V files)
                fileName = Path.GetFileName(file);
                filePathName = upldfldrpath + @"\" + fileName;
                System.IO.File.Copy(file, filePathName);
            }
            ZipFile.CreateFromDirectory(upldfldrpath, zipfile);
        }


        /// <summary>
        /// Upload the zip file.
        /// </summary>
        private async static Task<dynamic> UploadZipFile(string bucketKey)
        {
            ObjectsApi objects = new ObjectsApi();
            objects.Configuration.AccessToken = InternalToken.access_token;
            dynamic uploadedObj = null;
            string filename = Path.GetFileName(zipfile);

            using (StreamReader streamReader = new StreamReader(zipfile))
            {
                uploadedObj = await objects.UploadObjectAsync(bucketKey,
                      filename, (int)streamReader.BaseStream.Length, streamReader.BaseStream,
                      "application/octet-stream");
            }
            return uploadedObj;
        }


        /// <summary>
        /// Translate the uploaded zip file.
        /// </summary>
        private async static Task<dynamic> TranslateZipFile(dynamic newObject)
        {
            string objectIdBase64 = ToBase64(newObject.objectId);
            string rootfilename = Path.GetFileName(fileList[0]);
            List<JobPayloadItem> postTranslationOutput = new List<JobPayloadItem>()
                {
                    new JobPayloadItem(
                    JobPayloadItem.TypeEnum.Svf,
                    new List<JobPayloadItem.ViewsEnum>()
                    {
                        JobPayloadItem.ViewsEnum._3d,
                        JobPayloadItem.ViewsEnum._2d
                    })
                };        

            JobPayload postTranslation = new JobPayload(
                new JobPayloadInput(objectIdBase64,true, rootfilename),
                new JobPayloadOutput(postTranslationOutput));
            DerivativesApi derivativeApi = new DerivativesApi();
            derivativeApi.Configuration.AccessToken = InternalToken.access_token;
            dynamic translation = await derivativeApi.TranslateAsync(postTranslation);

            // check if it is complete.
            int progress = 0;
            do
            {
                System.Threading.Thread.Sleep(1000); // wait 1 second
                try
                {
                    dynamic manifest = await derivativeApi.GetManifestAsync(objectIdBase64);
                    progress = (string.IsNullOrWhiteSpace(Regex.Match(manifest.progress, @"\d+").Value) ? 100 : Int32.Parse(Regex.Match(manifest.progress, @"\d+").Value));
                }
                catch (Exception) { }
            } while (progress < 100);            
            return translation;
        }

        /// <summary>
        /// Get access token with public (read-only) scope.
        /// </summary>
        private async static Task<dynamic> GetPublicAsync()
        {
            if (PublicToken == null || PublicToken.ExpiresAt < DateTime.UtcNow)
            {
                PublicToken =  await Get2LeggedTokenAsync(new Scope[] { Scope.ViewablesRead });
                PublicToken.ExpiresAt = DateTime.UtcNow.AddSeconds(PublicToken.expires_in);
            }            
            return PublicToken;
        }

        /// <summary>
        /// Get access token with internal (write) scope.
        /// </summary>
        private async static Task<dynamic> GetInternalAsync()
        {
            if (InternalToken == null || InternalToken.ExpiresAt < DateTime.UtcNow)
            {
                InternalToken = await Get2LeggedTokenAsync(new Scope[] { Scope.BucketCreate, Scope.BucketRead, Scope.DataRead, Scope.DataCreate });
                InternalToken.ExpiresAt = DateTime.UtcNow.AddSeconds(InternalToken.expires_in);
            }
            return InternalToken;
        }

        /// <summary>
        /// Get a 2 legged authentication token.
        /// </summary> 
        private async static Task<dynamic> Get2LeggedTokenAsync(Scope[] scopes)
        {

            TwoLeggedApi oauth = new TwoLeggedApi();
            string grantType = "client_credentials";
            dynamic bearer = await oauth.AuthenticateAsync(
              GetAppSetting("FORGE_CLIENT_ID"),
              GetAppSetting("FORGE_CLIENT_SECRET"),
              grantType,
              scopes);
            return bearer;
        }


        /// <summary>
        /// Create a bucket.
        /// </summary>        
        private async static Task<dynamic> CreateBucket()
        {
                string bucketKey = "forgeapp" + Guid.NewGuid().ToString("N").ToLower();
                PostBucketsPayload postBucket = new PostBucketsPayload(bucketKey, null, PostBucketsPayload.PolicyKeyEnum.Transient /* erase after 24h*/ );
                BucketsApi bucketsApi = new BucketsApi();
                bucketsApi.Configuration.AccessToken = InternalToken.access_token;  //bearer is returned from 2 legged token
                dynamic newBucket = await bucketsApi.CreateBucketAsync(postBucket);
                return newBucket;
        }

        /// <summary>
        /// Reads appsettings from web.config.
        /// </summary>
        private static string GetAppSetting(string settingKey)
        {
            string str = ConfigurationManager.AppSettings[settingKey];
            return str ;
        }

        /// <summary>
        /// Convert a string into Base64 (source http://stackoverflow.com/a/11743162).
        /// </summary>       
        private static string ToBase64(string input)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(input);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        /// <summary>
        /// View the translated file using html.
        /// </summary>
        private static void openViewer(string base64Urn)
        {
            Console.WriteLine("***** Opening SVF file in viewer with urn:" + base64Urn);
            string st = _html.Replace("__URN__", base64Urn).Replace("__ACCESS_TOKEN__", PublicToken.access_token);
            System.IO.File.WriteAllText("viewer.html", st);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("viewer.html"));
        }

        #region Viewer HTML

        private static readonly string _html = @"<!DOCTYPE html>
<html>
<head>
	<meta charset=""UTF-8"">
	<script src=""https://developer.api.autodesk.com/viewingservice/v1/viewers/three.min.css""></script>
	<link rel=""stylesheet"" href=""https://developer.api.autodesk.com/viewingservice/v1/viewers/style.min.css"" />
	<script src=""https://developer.api.autodesk.com/viewingservice/v1/viewers/viewer3D.min.js""></script>
</head>
<body onload=""initialize()"">
<div id=""viewer"" style=""position:absolute; width:90%; height:90%;""></div>
<script>
	function authMe () { return ('__ACCESS_TOKEN__') ; }
	function initialize () {
		var options ={
			'document' : ""urn:__URN__"",
			'env': 'AutodeskProduction',
			'getAccessToken': authMe
		} ;
		var viewerElement =document.getElementById ('viewer') ;
		//var viewer =new Autodesk.Viewing.Viewer3D (viewerElement, {}) ; / No toolbar
		var viewer =new Autodesk.Viewing.Private.GuiViewer3D (viewerElement, {}) ; // With toolbar
		Autodesk.Viewing.Initializer (options, function () {
			viewer.initialize () ;
			loadDocument (viewer, options.document) ;
		}) ;
	}
	function loadDocument (viewer, documentId) {
		// Find the first 3d geometry and load that.
		Autodesk.Viewing.Document.load (
			documentId,
			function (doc) { // onLoadCallback
				var geometryItems =[] ;
				geometryItems =Autodesk.Viewing.Document.getSubItemsWithProperties (
					doc.getRootItem (),
					{ 'type' : 'geometry', 'role' : '3d' },
					true
				) ;
				if ( geometryItems.length <= 0 ) {
					geometryItems =Autodesk.Viewing.Document.getSubItemsWithProperties (
						doc.getRootItem (),
						{ 'type': 'geometry', 'role': '2d' },
						true
					) ;
				}
				if ( geometryItems.length > 0 )
					viewer.load (
						doc.getViewablePath (geometryItems [0])//,
						//null, null, null,
						//doc.acmSessionId /*session for DM*/
					) ;
			},
			function (errorMsg) { // onErrorCallback
				alert(""Load Error: "" + errorMsg) ;
			}//,
			//{
			//	'oauth2AccessToken': authMe (),
			//	'x-ads-acm-namespace': 'WIPDM',
			//	'x-ads-acm-check-groups': 'true',
			//}
		) ;
	}
</script>
</body>
</html>";

        #endregion

    }

}




