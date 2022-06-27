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
    public class ConfigValidator
    {
        public bool isValid = true; 
        public Logger logger;
        public MetadataConfig config;
        MFilesConnectionHandler handler;
        string validationMessage="";

        public ConfigValidator(Logger log, MFilesConnectionHandler handler)
        {
            this.logger = log;
            this.config = new MetadataConfig();
            this.handler = handler;
        }

        public void validateConfig()
        {
            logger.LogToFile("Validating the Default Properties Section", "Info");
            try
            {
                logger.LogToFile("Validating the Default Properties Section", "Debug");
                Dictionary<string, string> DefaultProperties = (ConfigurationManager.GetSection("MetadataConfigurations/DefaultProperties") as System.Collections.Hashtable)
                     .Cast<System.Collections.DictionaryEntry>()
                     .ToDictionary(n => n.Key.ToString(), n => n.Value.ToString());
                SetupDefaultProperties(DefaultProperties);


            }
            catch (Exception ex)
            {
                isValid = false;
                logger.LogToFile("There is an error in the Default Properties Section: " + ex.Message, "Info");
            }

            try
            {
                logger.LogToFile("Validating the Folder Level Properties Section","Debug");
                Dictionary<string, string> FolderLevelProperties = (ConfigurationManager.GetSection("MetadataConfigurations/FolderLevelProperties") as System.Collections.Hashtable)
                 .Cast<System.Collections.DictionaryEntry>()
                 .ToDictionary(n => n.Key.ToString(), n => n.Value.ToString());
                SetUpFolderLevelProperties(FolderLevelProperties);

            }
            catch (Exception ex)
            {
                isValid = false;
                logger.LogToFile("There is an error in the Folder Level Properties Section: " + ex.Message, "Info");
            }
            try
            {
                logger.LogToFile("Validating the Conditional Properties Section", "Debug");
                Dictionary<string, Dictionary<string, List<PropertyInfo>>> conprops = new Dictionary<string, Dictionary<string, List<PropertyInfo>>>();
                
                System.Configuration.Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                List<ConfigurationSectionGroup> sectionItems = config.SectionGroups.Cast<ConfigurationSectionGroup>().ToList();
                ConfigurationSectionGroup ConditionalPropertiesSection = sectionItems.Where(a => a.Name == "MetadataConfigurations").FirstOrDefault()
                                                    .SectionGroups.Cast<ConfigurationSectionGroup>().ToList()
                                                    .Where(a => a.Name == "ConditionalProperties").First();

                //foreach (ConfigurationSection section in ConditionalPropertiesSection.Sections)
                //{
                //    string sectionName= "MetadataConfigurations/ConditionalProperties/" + section.SectionInformation.Name;

                //    Dictionary<string, string> ConditionalProperties = (ConfigurationManager.GetSection(sectionName) as System.Collections.Hashtable)
                //                                                 .Cast<System.Collections.DictionaryEntry>()
                //                                                 .ToDictionary(n => n.Key.ToString(), n => n.Value.ToString());
                //    SetUpConditionalProperties(section.SectionInformation.Name, ConditionalProperties);
                //}
                SetUpConditionalProperties(ConditionalPropertiesSection);


            }
            catch (Exception ex)
            {
                isValid = false;
                logger.LogToFile("There is an error in the Conditional Properties Section: " + ex.Message, "Info");
            }
        }

        private void SetupDefaultProperties(Dictionary<string, string> DefaultProperties)
        {
            Dictionary<string, PropertyInfo> DefaultProps = new Dictionary<string, PropertyInfo>();
            foreach (KeyValuePair<string, string> pair in DefaultProperties)
            {
                PropertyInfo propInfo= getPropertyInfo(pair, false);
                DefaultProps.Add(pair.Key, propInfo);
            }
            config.DefaultProperties = DefaultProps;
        }

        private void SetUpFolderLevelProperties(Dictionary<string, string> FolderProperties)
        {
            Dictionary<int, PropertyInfo> FolderProps = new Dictionary<int, PropertyInfo>();
            foreach (KeyValuePair<string, string> pair in FolderProperties)
            {
                PropertyInfo propInfo = getPropertyInfo(pair, true);
                FolderProps.Add(Int32.Parse(pair.Value) , propInfo);
            }
            config.flps = FolderProps;
        }

        private void SetUpConditionalProperties(ConfigurationSectionGroup ConditionalPropertiesSectionGroup)
        {
            Dictionary<string, Dictionary<string, List<PropertyInfo>>> ConditionalPropertiesDictionary = new Dictionary<string, Dictionary<string, List<PropertyInfo>>>(); ;
            foreach (ConfigurationSection section in ConditionalPropertiesSectionGroup.Sections)
            {
                string sectionName = "MetadataConfigurations/ConditionalProperties/" + section.SectionInformation.Name;

                Dictionary<string, string> ConditionalPropertiesSection = (ConfigurationManager.GetSection(sectionName) as System.Collections.Hashtable)
                                                             .Cast<System.Collections.DictionaryEntry>()
                                                             .ToDictionary(n => n.Key.ToString(), n => n.Value.ToString());
                // SetUpConditionalProperties(section.SectionInformation.Name, ConditionalProperties);

                Dictionary<string, List<PropertyInfo>> ConditionalProps = new Dictionary<string, List<PropertyInfo>>();

                foreach (KeyValuePair<string, string> sectionItem in ConditionalPropertiesSection)
                {
                    string[] PropAndVal = sectionItem.Key.Split('|');
                    KeyValuePair<string, string> info = new KeyValuePair<string, string>(PropAndVal[0], sectionItem.Value);
                    PropertyInfo ConditionalPropertyToDefault = getPropertyInfo(info, false);

                    if (ConditionalProps.ContainsKey(PropAndVal[1]))
                    {
                        ConditionalProps[PropAndVal[1]].Add(ConditionalPropertyToDefault);
                    }
                    else
                    {
                        List<PropertyInfo> item = new List<PropertyInfo>();
                        item.Add(ConditionalPropertyToDefault);
                        ConditionalProps.Add(PropAndVal[1], item);
                    }
                    
                }
                ConditionalPropertiesDictionary.Add(section.SectionInformation.Name, ConditionalProps);
            }
            config.ConditionalPropertiess = ConditionalPropertiesDictionary;
        }

        private PropertyInfo getPropertyInfo(KeyValuePair<string, string> pair, bool isFolderLevelProp)
        {
            logger.LogToFile($"Getting vault info for the {pair.Value} property ", "Debug");
            PropertyInfo propertyInfo = new PropertyInfo();
            try
            {
                int defID;
                bool success = int.TryParse(pair.Key, out defID);
                if (!success)
                {
                    defID = handler.vault.PropertyDefOperations.GetPropertyDefIDByAlias(pair.Key);
                }

                logger.LogToFile($"Retrieving property with DefID {defID}", "Debug");
                // get the property from the vault
                PropertyDef property = handler.vault.PropertyDefOperations.GetPropertyDef(defID);

                // set all info about the property 
                propertyInfo.DefID = defID;
                propertyInfo.dataType = property.DataType;
                propertyInfo.PropertyName = property.Name;
                propertyInfo.StringValue = pair.Value;

                if (!success)
                {
                    propertyInfo.Alias = pair.Key;
                }
                else {
                    propertyInfo.Alias = handler.vault.PropertyDefOperations.GetPropertyDefAdmin(defID).SemanticAliases.Value;
                }

                if (property.BasedOnValueList)
                {
                    propertyInfo.ParentValueListID = property.ValueList;
                    if (isFolderLevelProp == false)
                    {
                        propertyInfo.itemID = SearchForVLItemByValue(property.ValueList, pair.Value);
                    }
                }
                if (property.BasedOnValueList && propertyInfo.itemID == -1 && isFolderLevelProp==false)
                {
                    throw new Exception($"The value set in the config file for the following property is Invalid {propertyInfo.PropertyName}, Value {propertyInfo.StringValue}.");
                }
                logger.LogToFile($"Property Info: Defid={defID}, {Environment.NewLine}Datatype={propertyInfo.dataType.ToString()},{Environment.NewLine}PropertyName: {propertyInfo.PropertyName}, {Environment.NewLine}Value={propertyInfo.StringValue}, {Environment.NewLine}isValuelist ={property.BasedOnValueList}, {Environment.NewLine}ParentVLID ={propertyInfo.ParentValueListID}, {Environment.NewLine}ItemID={propertyInfo.itemID}, , {Environment.NewLine}Alias={propertyInfo.Alias}.", "Debug");

            }
            catch (Exception ex)
            {
                isValid = false;
                logger.LogToFile(ex.Message, "info");
            }

            return propertyInfo;
        }

        private int SearchForVLItemByValue(int ValueList_DefID, string ItemValue)
        {
            int agencyID = -1;


            //   'search condition for the text value we are looking for 

            SearchConditions oSearchConditions = new SearchConditions();

            SearchCondition oVLItemSearchCondition = new SearchCondition();
            oVLItemSearchCondition.Expression.SetValueListItemExpression(MFilesAPI.MFValueListItemPropertyDef.MFValueListItemPropertyDefName, MFParentChildBehavior.MFParentChildBehaviorNone, null);
            oVLItemSearchCondition.ConditionType = MFConditionType.MFConditionTypeStartsWith;
            oVLItemSearchCondition.TypedValue.SetValue(MFDataType.MFDatatypeText, ItemValue);
            oSearchConditions.Add(-1, oVLItemSearchCondition);


            // 'get only non deleted items

            SearchCondition NotDeletedCondition = new SearchCondition();

            NotDeletedCondition.Expression.SetValueListItemExpression(MFValueListItemPropertyDef.MFValueListItemPropertyDefDeleted, MFParentChildBehavior.MFParentChildBehaviorNone, null);
            NotDeletedCondition.ConditionType = MFConditionType.MFConditionTypeEqual;

            NotDeletedCondition.TypedValue.SetValue(MFDataType.MFDatatypeBoolean, false);

            oSearchConditions.Add(-1, NotDeletedCondition);

            ValueListItemSearchResults Results = handler.vault.ValueListItemOperations.SearchForValueListItemsEx(ValueList_DefID, oSearchConditions);

            if (Results.Count > 0)
            {
                agencyID = Results[1].ID;

            }


            return agencyID;
        }


    }
}

