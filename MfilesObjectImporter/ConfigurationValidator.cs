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
    public class ConfigurationValidator
    {
        public bool isValid = true;
        public Logger logger;
        public MetadataConfig config;
        public MigrationConfigurations xmlFileSettings;
        public MFilesConnectionHandler handler;
        string validationMessage = "";
        Dictionary<string, int> aliasLookup;

        public ConfigurationValidator(Logger log, MigrationConfigurations settings)
        {
            this.logger = log;
            this.config = new MetadataConfig();
            this.xmlFileSettings = settings;
            this.handler = new MFilesConnectionHandler(logger, settings.AppSettings);
            this.aliasLookup = new Dictionary<string, int>();
        }

        public void validateConfig()
        {
           
            // validate default settings section
            try
            {
                logger.LogToFile("Validating the Vault settings", "Info");
                //VaultSettings VaultSettings = xmlFileSettings.vaultSettings;
                //handler = new MFilesConnectionHandler(logger, VaultSettings);
                handler.connectToMfilesVault();
                config.ApplicationSettings = xmlFileSettings.AppSettings;
            }
            catch (Exception ex)
            {
                isValid = false;
                logger.LogToFile("There is an error connecting to the vault: " + ex.Message, "Info");
            }

            // validate the default settings 
            try
            {
                logger.LogToFile("Validating the Default Settings", "Info");
                SetupDefaultProperties(xmlFileSettings.defaultPropertiesSettings);
            }
            catch (Exception ex)
            {
                isValid = false;
                logger.LogToFile("There is an error in the Default Properties Section: " + ex.Message, "Info");
            }

            try
            {
                logger.LogToFile("Validating the Folder Level Settings", "Info");
                SetUpFolderLevelProperties(xmlFileSettings.FolderLevelSettings);
            }
            catch (Exception ex)
            {
                isValid = false;
                logger.LogToFile("There is an error in the Folder level settings Section: " + ex.Message, "Info");
            }
            //priority, parent, ifvalue, properties to set 
            try
            {
                logger.LogToFile("Validating the Conditional Property Settings", "Info");
                SetUpConditionalProperties(xmlFileSettings.ConditionalPropertySettings);               

            }
            catch (Exception ex)
            {
                isValid = false;
                logger.LogToFile("There is an error in the Conditional Properties Section: " + ex.Message, "Info");
            }
            // concatenated properties
            try
            {
                logger.LogToFile("Validating the concatenated Property settings", "Info");
                SetUpConcatenatedProperties(xmlFileSettings.concactenatedProperties);             

            }
            catch (Exception ex)
            {
                isValid = false;
                logger.LogToFile("There is an error in the Concatenated Properties Section: " + ex.Message, "Info");
            }


        }

        private void SetUpConcatenatedProperties(List<Property> concatSettings)
        {
            List<PropertyInfo> ConcatPropsList = new List<PropertyInfo>();
            foreach (Property prop in concatSettings)
            {
                PropertyInfo propInfo = getPropertyInfo(prop.idOrAlias.Trim(), prop.value.Trim(), false, -1);
                propInfo.Delimiter = prop.delimiter;
                ConcatPropsList.Add(propInfo);
            }
            config.concactenatedProperties = ConcatPropsList;
        }

        private void SetupDefaultProperties(List<Property> defaultPropertiesSettings)
        {
            Dictionary<string, PropertyInfo> DefaultProps = new Dictionary<string, PropertyInfo>();
            foreach (Property prop in defaultPropertiesSettings)
            {
                PropertyInfo propInfo = getPropertyInfo(prop.idOrAlias.Trim(), prop.value.Trim(), false, -1);
                DefaultProps.Add(prop.idOrAlias.Trim(), propInfo);
                if (propInfo.Alias.Trim() != "" && !aliasLookup.ContainsKey(propInfo.Alias))
                {
                    aliasLookup.Add(propInfo.Alias, propInfo.DefID);
                }
            }
            config.DefaultProperties = DefaultProps;
        }

        private void SetUpFolderLevelProperties(List<FolderSetting> FolderLevelSettings)
        {
            // organized by folder id , then by string value to search for, property to set 
            Dictionary<int, Dictionary<string, PropertyInfo>> FolderProps = new Dictionary<int, Dictionary<string, PropertyInfo>>();
            foreach (FolderSetting settingItem in FolderLevelSettings)
            {
                KeyValuePair<string, string> pair;
                if (settingItem.hasConditions == false)
                {
                    PropertyInfo nonConditionalPropInfo = getPropertyInfo(settingItem.idOrAlias.Trim(), settingItem.folderLevel.ToString(), true, -1);
                    nonConditionalPropInfo.isRequired = settingItem.isRequired;
                    nonConditionalPropInfo.exactMatch = settingItem.exactMatch;
                    nonConditionalPropInfo.updateOnTheFly = true;
                    Dictionary<string, PropertyInfo> folderlvlpair = new Dictionary<string, PropertyInfo>();
                    folderlvlpair.Add("*", nonConditionalPropInfo);
                    FolderProps.Add(settingItem.folderLevel, folderlvlpair);
                }
                else {
                     Dictionary<string, PropertyInfo> folderlvlpair = new Dictionary<string, PropertyInfo>();
                    foreach (Condition folderCondition in settingItem.ValueConditions)
                    {
                        string[] containsValueArray = folderCondition.ifcontainsValue.Split('|');
                        foreach (string value in containsValueArray)
                        {
                            if (!folderlvlpair.ContainsKey(value))
                            {
                                string containsValue = value;
                                string valueToSet = folderCondition.valueToSet;
                                PropertyInfo conditionalPropInfo = getPropertyInfo(settingItem.idOrAlias.Trim(), folderCondition.valueToSet.Trim(), false, -1);
                                conditionalPropInfo.isRequired = settingItem.isRequired;
                                conditionalPropInfo.exactMatch = settingItem.exactMatch;
                                folderlvlpair.Add(value, conditionalPropInfo);
                            }
                            else
                            {
                                logger.LogToFile($"There is already a folder level item for value: {folderCondition.ifcontainsValue} at folder level {settingItem}", "Info");
                            }
                        }                        
                    }
                    FolderProps.Add(settingItem.folderLevel, folderlvlpair);
                }
            }
            config.FolderLevelProperties = FolderProps;
        }

        private void SetUpConditionalProperties(List<ConditionalProperty> ConditionalSettings)
        {
            List<ConditionalProperty> orderedList = ConditionalSettings.OrderBy(x => x.ProcessingOrder).ToList();
            foreach (ConditionalProperty prop in orderedList)
            {                
                try
                {                    
                    foreach (ConditionalSettings setting in prop.ParentChildConditions)
                    {   
                        PropertyInfo parentPropInfo = getPropertyInfo(prop.parentProperty.Trim(), setting.hasValue.ToString(), false, -1);
                        
                        PropertyInfo ChildPropInfo;
                        if (setting.AditionalCriteria.Count == 0 || setting.AditionalCriteria == null )
                        {                
                            if (setting.ParentPropertyisVLOwner)
                            {
                                ChildPropInfo = getPropertyInfo(setting.propertyToSet.Trim(), setting.valueToSet.ToString(), false, parentPropInfo.itemID);
                                if (parentPropInfo.itemID == -1)
                                {
                                    ChildPropInfo.itemID = -1;
                                }
                            }
                            else {
                                ChildPropInfo = getPropertyInfo(setting.propertyToSet.Trim(), setting.valueToSet.ToString(), false, -1);
                            }
                            setting.propToSetPropInfo = ChildPropInfo;
                        }
                        else {
                            foreach (Criteria criteria in setting.AditionalCriteria)
                            {
                                if (setting.ParentPropertyisVLOwner)
                                {
                                    ChildPropInfo = getPropertyInfo(setting.propertyToSet.Trim(), criteria.valueToSet.ToString(), false, parentPropInfo.itemID);
                                    criteria.propToSetPropInfo = ChildPropInfo;
                                }
                                else
                                {
                                    ChildPropInfo = getPropertyInfo(setting.propertyToSet.Trim(), criteria.valueToSet.ToString(), false, -1);
                                    criteria.propToSetPropInfo = ChildPropInfo;
                                }
                            }
                        
                        }
                    }
                }                
                catch (Exception ex)
                {
                    isValid = false;
                    logger.LogToFile($"Error with conditional property of ParentProperty {prop.parentProperty}: Message: {ex.Message}", "info");
                }

            }
            config.ConditionalProperties = orderedList;
        }


        // takes in a pair that includes "aliasOrID" and "value of property", 
        public PropertyInfo getPropertyInfo( string AliasOrID, string valueToSet, bool isUnknownValueFolderProperty, int ParentItemID)
        {
            logger.LogToFile($"Getting vault info for the property with ID: {AliasOrID} ", "Debug");
            PropertyInfo propertyInfo = new PropertyInfo();
            try
            {
                int defID;
                bool success = int.TryParse(AliasOrID, out defID);
                if (!success)
                {
                    defID = handler.vault.PropertyDefOperations.GetPropertyDefIDByAlias(AliasOrID);
                }

                logger.LogToFile($"Retrieving property with DefID {defID}", "Debug");
                // get the property from the vault
                PropertyDef property = handler.vault.PropertyDefOperations.GetPropertyDef(defID);

                // set all info about the property 
                propertyInfo.DefID = defID;
                propertyInfo.dataType = property.DataType;
                propertyInfo.PropertyName = property.Name;
                propertyInfo.StringValue = valueToSet;

                if (!success)
                {
                    propertyInfo.Alias = AliasOrID;
                }
                else
                {
                    propertyInfo.Alias = handler.vault.PropertyDefOperations.GetPropertyDefAdmin(defID).SemanticAliases.Value;
                }

                if (property.BasedOnValueList)
                {
                    propertyInfo.ParentValueListID = property.ValueList;
                    if (!isUnknownValueFolderProperty == true && propertyInfo.StringValue !="*")
                    {
                        propertyInfo.itemID = SearchForVLItemByValue(property.ValueList, valueToSet, ParentItemID);
                    }
                }
                if (property.BasedOnValueList && propertyInfo.itemID == -1 && isUnknownValueFolderProperty == false && propertyInfo.StringValue !="*")
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

        private int SearchForVLItemByValue(int ValueList_DefID, string ItemValue, int parentItemID)
        {
            int itemID = -1;


            //   'search condition for the text value we are looking for 

            SearchConditions oSearchConditions = new SearchConditions();
            SearchCondition oVLItemSearchCondition = new SearchCondition();
            oVLItemSearchCondition.Expression.SetValueListItemExpression(MFilesAPI.MFValueListItemPropertyDef.MFValueListItemPropertyDefName, MFParentChildBehavior.MFParentChildBehaviorNone, null);
            oVLItemSearchCondition.ConditionType = MFConditionType.MFConditionTypeEqual;
            oVLItemSearchCondition.TypedValue.SetValue(MFDataType.MFDatatypeText, ItemValue.Trim());
            oSearchConditions.Add(-1, oVLItemSearchCondition);


            // 'get only non deleted items
            SearchCondition NotDeletedCondition = new SearchCondition();
            NotDeletedCondition.Expression.SetValueListItemExpression(MFValueListItemPropertyDef.MFValueListItemPropertyDefDeleted, MFParentChildBehavior.MFParentChildBehaviorNone, null);
            NotDeletedCondition.ConditionType = MFConditionType.MFConditionTypeEqual;
            NotDeletedCondition.TypedValue.SetValue(MFDataType.MFDatatypeBoolean, false);
            oSearchConditions.Add(-1, NotDeletedCondition);

            //parent Item ID 
            if (parentItemID > 0)
            {
                SearchCondition parentCondition = new SearchCondition();
                parentCondition.Expression.SetValueListItemExpression(MFValueListItemPropertyDef.MFValueListItemPropertyDefOwner, MFParentChildBehavior.MFParentChildBehaviorNone, null);
                parentCondition.ConditionType = MFConditionType.MFConditionTypeEqual;
                parentCondition.TypedValue.SetValue(MFDataType.MFDatatypeLookup, parentItemID);
                oSearchConditions.Add(-1, parentCondition);
            }

            ValueListItemSearchResults Results = handler.vault.ValueListItemOperations.SearchForValueListItemsEx(ValueList_DefID, oSearchConditions);

            if (Results.Count > 0)
            {
                itemID = Results[1].ID;

            }
            return itemID;
        }
    }
}
