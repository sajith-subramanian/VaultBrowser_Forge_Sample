using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using VDF = Autodesk.DataManagement.Client.Framework;

namespace VaultBrowserSample
{
    class Program
    {
        private static VDF.Vault.Currency.Connections.Connection m_conn;

        static void Main(string[] args)
        {
            VDF.Vault.Results.LogInResult lr =
            VDF.Vault.Library.ConnectionManager.LogIn
            ("localhost", "TestVault", "administrator", "", VDF.Vault.Currency.Connections.AuthenticationFlags.ReadOnly, null);

            if (lr.Success)
            {
                m_conn = lr.Connection;
                VDF.Vault.Forms.Settings.SelectEntitySettings settings =
                   new VDF.Vault.Forms.Settings.SelectEntitySettings();

                VDF.Vault.Forms.Settings.SelectEntitySettings.EntityRegularExpressionFilter[] filters =
                    new VDF.Vault.Forms.Settings.SelectEntitySettings.EntityRegularExpressionFilter[]
                    {
                        new VDF.Vault.Forms.Settings.SelectEntitySettings.EntityRegularExpressionFilter("Assembly Files (*.iam)", ".+iam", VDF.Vault.Currency.Entities.EntityClassIds.Files),
                        new VDF.Vault.Forms.Settings.SelectEntitySettings.EntityRegularExpressionFilter("Part Files (*.ipt)", ".+ipt", VDF.Vault.Currency.Entities.EntityClassIds.Files)
                    };

                VDF.Vault.Forms.Controls.VaultBrowserControl.Configuration initialConfig = new VDF.Vault.Forms.Controls.VaultBrowserControl.Configuration(m_conn, settings.PersistenceKey, null);

                initialConfig.AddInitialColumn(VDF.Vault.Currency.Properties.PropertyDefinitionIds.Server.EntityName);
                initialConfig.AddInitialColumn(VDF.Vault.Currency.Properties.PropertyDefinitionIds.Server.CheckInDate);
                initialConfig.AddInitialColumn(VDF.Vault.Currency.Properties.PropertyDefinitionIds.Server.Comment);
                initialConfig.AddInitialColumn(VDF.Vault.Currency.Properties.PropertyDefinitionIds.Server.ThumbnailSystem);
                initialConfig.AddInitialSortCriteria(VDF.Vault.Currency.Properties.PropertyDefinitionIds.Server.EntityName, true);

                settings.DialogCaption = "Select Part or Assembly file to Upload";
                settings.ActionableEntityClassIds.Add("FILE");
                settings.MultipleSelect = false;
                settings.ConfigureActionButtons("Upload", null, null, false);                
                settings.ConfigureFilters("Applied filter", filters, null);
                settings.OptionsExtensibility.GetGridConfiguration = e => initialConfig;

                Console.WriteLine("Launching Vault Browser...");
                VDF.Vault.Forms.Results.SelectEntityResults results =
                    VDF.Vault.Forms.Library.SelectEntity(m_conn, settings);
                if (results != null)
                {
                    VDF.Vault.Currency.Entities.FileIteration fileIter = results.SelectedEntities.FirstOrDefault() as VDF.Vault.Currency.Entities.FileIteration;
                    string result = Util.DownloadFilestoFolder(fileIter, m_conn);
                    if (result != "Directory not found")
                    {
                        Task t = Task.Run((Util.UploadAssembly));
                        t.Wait();
                    }
                }
            }
        }
    }
}
