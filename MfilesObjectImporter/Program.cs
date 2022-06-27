using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Xml;
using System.IO;

namespace MfilesObjectImporter
{
    class Program
    {
        static int indentLevel = 0;
        static void Main(string[] args)
        {
            // CODE TO KEEP 
            Serializer ser = new Serializer();
            string xmlInputData = string.Empty;
            string path = ConfigurationManager.AppSettings["ConfigFileLocation"];

            xmlInputData = File.ReadAllText(path);

            MigrationConfigurations settings = ser.Deserialize<MigrationConfigurations>(xmlInputData);

            Logger logger =new Logger();

            //new validation way 
            ConfigurationValidator validator = new ConfigurationValidator(logger, settings);
            validator.validateConfig();

            // process the files
            if (validator.isValid)
            {
                FileProcessor fp = new FileProcessor(logger, validator, validator.config, validator.handler);
                fp.ProcessDirectory();
            }
            else {
                logger.LogToFile("Please Resolve all errors in config file before processing", "info");
            }
            Console.ReadLine();
        }

        static void ShowSectionGroupCollectionInfo(
            ConfigurationSectionGroupCollection sectionGroups)
        {
            foreach (ConfigurationSectionGroup sectionGroup in sectionGroups)
            {
                ShowSectionGroupInfo(sectionGroup);
            }
        }
        static void ShowSectionGroupInfo(
            ConfigurationSectionGroup sectionGroup)
        {
            // Get the section group name.
            indent("Section Group Name: " + sectionGroup.Name);

            // Get the fully qualified group name.
            indent("Section Group Name: " + sectionGroup.SectionGroupName);

            indentLevel++;

            indent("Type: " + sectionGroup.Type);
            indent("Is Group Required?: " +
                sectionGroup.IsDeclarationRequired);
            indent("Is Group Declared?: " + sectionGroup.IsDeclared);
            indent("Contained Sections:");

            indentLevel++;
            foreach (ConfigurationSection section
                in sectionGroup.Sections)
            {
                indent("Section Name:" + section.SectionInformation.Name);
            }
            indentLevel--;

            // Display contained section groups if there are any.
            if (sectionGroup.SectionGroups.Count > 0)
            {
                indent("Contained Section Groups:");

                indentLevel++;
                ConfigurationSectionGroupCollection sectionGroups =
                    sectionGroup.SectionGroups;
                ShowSectionGroupCollectionInfo(sectionGroups);
            }

            Console.WriteLine("");
            indentLevel--;
        }

        static void indent(string text)
        {
            for (int i = 0; i < indentLevel; i++)
            {
                Console.Write("  ");
            }
            Console.WriteLine(text.Substring(0, Math.Min(79 - indentLevel * 2, text.Length)));
        }
    }
}
