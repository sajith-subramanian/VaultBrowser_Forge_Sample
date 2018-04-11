# VaultBrowser_Forge_Sample
Vault Forge sample that gets an Inventor Assembly or Part from Vault and uploads it to a bucket and renders it using the Viewer.

This an updated version of the previous VaultBrowserSample_Forge_Demo. 

Some key highlights of this example:

-- This example does a read only sign-in, so the clmloader.dll is not required.

-- Is not really based off the Vault Browser sample in the SDK, contains lesser code. 

-- No longer uses Apprentice Server so no need to have Inventor View / Inventor installed.

-- You will need to change the code so that it logs into your vault. (Line #19 in Program.cs).

-- The example creates the following 3 folders in the user's temp directory:
   - Vault_Forge_Downloaded_Files
   - Vault_Forge_Files_To_Upload
   - Vault_Forge_zip
