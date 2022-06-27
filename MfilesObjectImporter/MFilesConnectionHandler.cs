using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MFilesAPI;
using System.Configuration;
using System.IO;

namespace MfilesObjectImporter
{
    public class MFilesConnectionHandler
    {
        public MFilesServerApplication serverApplication;
        public Vault vault;
        Logger logger;
        AppSettings vaultXmlsettings;

        public MFilesConnectionHandler(Logger log, AppSettings vaultXMLsettings)
        {
            this.logger = log;
            this.vaultXmlsettings = vaultXMLsettings;
        }

        public void connectToMfilesVault()
        {
            logger.LogToFile("Connecting to Vault", "Info");

            serverApplication = new MFilesServerApplication();

            //string AuthType = ConfigurationManager.AppSettings["EmbedDocTypes"];
            string username = vaultXmlsettings.username.Trim();
            string password = vaultXmlsettings.password.Trim();
            string protocol = vaultXmlsettings.protocol.Trim();
            string server = vaultXmlsettings.server.Trim();
            string port = vaultXmlsettings.port.ToString().Trim();
            string VaultGUID = vaultXmlsettings.vaultGUID.Trim();

            // connect to the server
            MFAuthType AuthType = getMFAuthType();
            serverApplication.Connect(AuthType, username, password, "", protocol, server, port, "", false);
            vault = serverApplication.LogInToVault(VaultGUID);
            logger.LogToFile("Connected to Vault: " + vault.Name, "info");
        }

        public void connectToMfilesVaultfromAppConfig()
        {
            logger.LogToFile("Connecting to Vault", "Info");

            serverApplication = new MFilesServerApplication();

            //string AuthType = ConfigurationManager.AppSettings["EmbedDocTypes"];
            string username = ConfigurationManager.AppSettings["username"];
            string password = ConfigurationManager.AppSettings["password"];
            string protocol = ConfigurationManager.AppSettings["protocol"];
            string server = ConfigurationManager.AppSettings["server"];
            string port = ConfigurationManager.AppSettings["port"];
            string VaultGUID = ConfigurationManager.AppSettings["VaultGUID"];

            // connect to the server
            MFAuthType AuthType = getMFAuthType();
            serverApplication.Connect(AuthType, username, password, "", protocol, server, port, "", false);
            vault = serverApplication.LogInToVault(VaultGUID);
            logger.LogToFile("Connected to Vault: " + vault.Name, "info");
        }
        public MFAuthType getMFAuthType()
        {
            MFAuthType type = MFAuthType.MFAuthTypeSpecificMFilesUser;

            if (ConfigurationManager.AppSettings["SpecificMFilesUser"] == "false")
            {
                type = MFAuthType.MFAuthTypeSpecificWindowsUser;
            }
            return type;
        }
    }
}
