using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.IO;
using MFilesAPI;
using System.Collections;

namespace MfilesObjectImporter
{
    public class FileProcessor
    {
        Logger logger;
        MFilesConnectionHandler handler;
        MetadataConfig config;
        ConfigurationValidator validator;
        int filecount = 0;
        int ProcessCount = 0;
        List<string> excludedExtensions;
        string CurrentFilePath = "";
        bool missingRequiredProp = false;
        Dictionary<int, string> propertiesSet;
        bool deleteFiles = false;
        public FileProcessor(Logger log, ConfigurationValidator validator, MetadataConfig config, MFilesConnectionHandler handler)
        {
            this.logger = log;
            this.config = config;
            this.handler = handler;
            this.validator = validator;
            excludedExtensions = config.ApplicationSettings.ExcludedExtensions.Split('|').ToList();
            try
            {
                if (ConfigurationManager.AppSettings["DeleteMigratedFiles"].ToLower() == "true")
                {
                    this.deleteFiles = true;
                }
            }
            catch { }
        }



        public void ProcessDirectory()
        {
            String DirectoryToProcess = config.ApplicationSettings.StartFolderLocation;
               
            if (Directory.Exists(DirectoryToProcess))
            {
                try
                {
                    getFileCountInDir(DirectoryToProcess);
                    getAllFiles(DirectoryToProcess);
                }
                catch (Exception ex)
                {
                    logger.LogToFile("There is an error processing the specified directory: " + ex.Message, "Info");
                }
            }      
        }

        public void getFileCountInDir(string Dir)
        {
            if (Directory.Exists(Dir))
            {
                string[] filesinDir = Directory.GetFiles(Dir);
                filecount = filecount + filesinDir.Count();
            }

            string[] directories = Directory.GetDirectories(Dir);

            foreach (string dir in directories)
            {
                getFileCountInDir(dir);       
            }
        }


        public void getAllFiles(string Dir)
        {
            if (Directory.Exists(Dir))
            {
                string[] filesinDir = Directory.GetFiles(Dir);
                foreach (string file in filesinDir)
                {

                    try
                    {
                        missingRequiredProp = false;
                        ProcessCount = ProcessCount + 1;
                        logger.LogToFile($"==========================================Processing {ProcessCount} or {filecount} Files========================================== ", "Info");
                        CurrentFilePath = file;
                        logger.LogToFile($"FileName: {CurrentFilePath}", "Info");
                        string extension = Path.GetExtension(file);
                        string name = Path.GetFileNameWithoutExtension(file);
                        if (excludedExtensions.Contains(extension) || name.StartsWith("~"))
                        {
                            if (name.StartsWith("~"))
                            {
                                logger.LogToFile($"Skipping File starting with ~ : {file} ", "Info");
                                logger.LogToSkippedFile(file, "debug");
                            }
                            else
                            {
                                logger.LogToFile($"Skipping File, extension in exclusion list: {file} " , "Info");
                                logger.LogToSkippedFile(file, "debug");
                            }
                        }
                        else
                        {
                            ProcessFile();
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogToFile($"There is an error processing the following file: {file} " + ex.Message, "Info");
                        logger.LogToFile($"Properties Settings:", "Info");
                        logger.LogToSkippedFile(CurrentFilePath, "debug");
                    }
                }
            }

            if (Directory.Exists(Dir))
            {
                string[] directories = Directory.GetDirectories(Dir);

                foreach (string dir in directories)
                {
                    try
                    {
                        if (Directory.Exists(dir))
                        {
                            getAllFiles(dir);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogToFile($"There is an error processing the following directory: {dir} " + ex.Message, "Info");
                    }

                }
            }
        }

        public void ProcessFile()
        {
            
            propertiesSet = new Dictionary<int, string>();
            PropertyValues propertyValues = new PropertyValues();
            try
            {
                processDefaultProperties(propertyValues);
                if (!missingRequiredProp)
                {
                    ProcessFolderLevelProperties(propertyValues);
                }
                if (!missingRequiredProp)
                {
                    ProcessConditionalProperties(propertyValues);
                }
                if (!missingRequiredProp)
                {
                    ProcessConcatenatedProperties(propertyValues);
                }
                if (!missingRequiredProp)
                {
                    CreateFileInMFiles(propertyValues);
                }
                else
                {
                    logger.LogToFile($"Skipping file due to missing requred property: ", "Info");
                    logger.LogToSkippedFile(CurrentFilePath, "debug");
                }
            }
            catch (Exception ex)
            {
                logger.LogToFile($"ERROR: There was an error processing the file, adding file to skipped Files log path: {CurrentFilePath}: {ex.Message} ", "Info");
                logger.LogToSkippedFile(CurrentFilePath, "debug");
            }
        }

        public void processDefaultProperties(PropertyValues propertyValues)
        {
            foreach (KeyValuePair<string, PropertyInfo> pair in config.DefaultProperties)
            {
                try
                {
                    PropertyValue propval = new PropertyValue();
                    propval = createPropertyValue(pair.Value, false);
                    propertyValues.Add(-1, propval);
                    addToGlobalPropertiesList(pair.Value);
                }
                catch (Exception ex)
                {
                    missingRequiredProp = true;
                    throw new Exception($"The Following default propperty is invalid: { pair.Value.PropertyName }. Setting required Prop missing = true" + ex.Message);
                }
            }
        }

        public void ProcessFolderLevelProperties(PropertyValues propertyValues)
        {
            string folderpath = CurrentFilePath.Replace(config.ApplicationSettings.StartFolderLocation.Trim(), "").TrimStart('\\');
            string[] FolderlevelArray = folderpath.Split('\\');
            try
            {
                foreach (KeyValuePair<int, Dictionary<string, PropertyInfo>> folderLevelPair in config.FolderLevelProperties)
                {
                    int folderLevel = folderLevelPair.Key;
                    string folderLevelValue = "";
                    bool foundProperty = false;
                    bool isrequired=false;
                    if (folderLevel <= FolderlevelArray.Count() - 1 && folderLevel > 0)
                    {
                        folderLevelValue = FolderlevelArray[folderLevel - 1];
                    }

                    PropertyInfo currentPropertyinfo = folderLevelPair.Value.Values.First();
                    if (currentPropertyinfo.isRequired == true)
                    {
                        isrequired = true;
                    }
                    // this means there is only one value and the index is a * which means that the value is unknown so it has to by updated on the fly 
                    if (folderLevelPair.Value.Values.Count == 1 && folderLevelPair.Value.Keys.First() == "*")
                    {
                        // setting value and found known value 
                        if (folderLevelValue != "")
                        {
                            PropertyInfo folderlevelPropInfo = validator.getPropertyInfo(currentPropertyinfo.Alias, folderLevelValue, false, -1);
                            if ((folderlevelPropInfo.dataType == MFDataType.MFDatatypeLookup && folderlevelPropInfo.itemID > -1) || (folderlevelPropInfo.dataType != MFDataType.MFDatatypeLookup && folderlevelPropInfo.itemID > 0))
                            {
                                foundProperty = true;
                                PropertyValue propval = createPropertyValue(folderlevelPropInfo, false);
                                propertyValues.Add(-1, propval);
                                addToGlobalPropertiesList(folderlevelPropInfo);
                            }

                        }

                    }
                    else
                    {// if it hits this there are probably additional conditions 
                        List<string> folderlevelconditionlist = folderLevelPair.Value.Keys.ToList();
                        if (folderLevelValue != "")
                        {
                            bool matchingCondition = false;
                            string keyToUse = "";
                            foreach (string searchPhrase in folderlevelconditionlist)
                            {
                                if (currentPropertyinfo.exactMatch == true)
                                {
                                    if (folderLevelValue == searchPhrase)
                                    {
                                        matchingCondition = true;
                                        keyToUse = searchPhrase;
                                        break;
                                    }
                                }
                                else
                                {
                                    if (folderLevelValue.Contains( searchPhrase))
                                    {
                                        matchingCondition = true;
                                        keyToUse = searchPhrase;
                                        break;
                                    }
                                }
                            }

                            if (matchingCondition == true)
                            {
                                foundProperty = true;
                                PropertyInfo conditionalPropInfo = folderLevelPair.Value[keyToUse];
                                PropertyValue propval = createPropertyValue(conditionalPropInfo, false);
                                propertyValues.Add(-1, propval);
                                addToGlobalPropertiesList(conditionalPropInfo);
                            }

                        }
                    }
                    if (isrequired && foundProperty == false)
                    {
                        missingRequiredProp = true;
                        logger.LogToFile($"ERROR: There is an missing required folder property: {currentPropertyinfo.PropertyName} ", "Info");
                        throw new Exception($"The Following required propperty is missing: { currentPropertyinfo.PropertyName }. Setting required Prop missing = true");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogToFile($"ERROR: There is an error adding a property: {ex.Message} ", "Info");
            }
        }

        public void ProcessConditionalProperties( PropertyValues propertyValues)
        {
            List<ConditionalProperty> conditionalProperties = config.ConditionalProperties;
            foreach (ConditionalProperty property in conditionalProperties)
            {
                string parentProperty = property.parentProperty;
                int parentDefid;
                bool success = int.TryParse(parentProperty, out parentDefid);
                if (!success)
                {
                    parentDefid = handler.vault.PropertyDefOperations.GetPropertyDefIDByAlias(parentProperty);
                }
                string parentValue = propertiesSet[parentDefid];
                if (property.ParentChildConditions.Count == 1 && property.ParentChildConditions.First().hasValue == "*")
                {
                    PropertyInfo parentPropInfo = validator.getPropertyInfo(parentProperty, parentValue, false, -1);
                    PropertyInfo childPropInfo = validator.getPropertyInfo(property.ParentChildConditions.First().propertyToSet, property.ParentChildConditions.First().valueToSet, false, parentPropInfo.itemID);
                    if (childPropInfo.itemID > -1)
                    {
                        PropertyValue newProperty = createPropertyValue(childPropInfo, false);
                        propertyValues.Add(-1, newProperty);
                        addToGlobalPropertiesList(childPropInfo);
                    }
                }
                else
                {
                    List<ConditionalSettings> filterdlist = property.ParentChildConditions.Where(a => a.hasValue == parentValue).ToList();
                    foreach (ConditionalSettings setting in filterdlist)
                    {
                        if (setting.AditionalCriteria.Count() == 0)
                        {
                                                      
                            if (!propertiesSet.ContainsKey(setting.propToSetPropInfo.DefID))
                            {
                                PropertyValue newProperty = createPropertyValue(setting.propToSetPropInfo, false);
                                propertyValues.Add(-1, newProperty);
                                addToGlobalPropertiesList(setting.propToSetPropInfo);
                            }
                            else
                            {
                                logger.LogToFile($"The following property: {setting.propToSetPropInfo.PropertyName} is already set to {propertiesSet[setting.propToSetPropInfo.DefID]}. There is a duplicate setting trying to set this property to {setting.propToSetPropInfo.StringValue}. Maintaining original value. ", "Info");
                            }
                        }
                        else
                        {
                            List<Criteria> criteriaList = setting.AditionalCriteria;
                            bool criteriaMet = false;
                            bool criteriaFailed = false;
                            foreach (Criteria option in criteriaList)
                            {
                                criteriaMet = false;
                                criteriaFailed = false;
                                List<FolderCriteria> folderCriteriasList = option.FolderCriterias;
                                List<PropertyCriteria> propertyCriteriaList = option.PropertyCriterias;
                                foreach (FolderCriteria crit in folderCriteriasList)
                                {
                                    string folderpath = CurrentFilePath.Replace(config.ApplicationSettings.StartFolderLocation.Trim(), "").TrimStart('\\');
                                    string[] FolderlevelArray = folderpath.Split('\\');

                                    if (crit.folderLevel <= FolderlevelArray.Count() - 1 && crit.folderLevel > 0)
                                    {
                                        string folderLevelValue = FolderlevelArray[crit.folderLevel - 1];
                                        if (!folderLevelValue.Contains(crit.ifcontainsValue))
                                        {
                                            criteriaFailed = true;
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        criteriaFailed = true;
                                        break;
                                    }
                                }
                                if (criteriaFailed != true)
                                {
                                    foreach (PropertyCriteria crit in propertyCriteriaList)
                                    {
                                        string IdorAlias = crit.idOrAlias;
                                        string value = crit.value;

                                        int propertyCriteriaDefid;
                                        bool successful = int.TryParse(IdorAlias, out propertyCriteriaDefid);
                                        if (!successful)
                                        {
                                            propertyCriteriaDefid = handler.vault.PropertyDefOperations.GetPropertyDefIDByAlias(IdorAlias);
                                        }
                                        string conditionalPropertyVal = propertiesSet[propertyCriteriaDefid];
                                        if (conditionalPropertyVal != crit.value)
                                        {
                                            criteriaFailed = true;
                                            break;
                                        }
                                    }

                                }
                                if (criteriaFailed != true)
                                {
                                    if (!propertiesSet.ContainsKey(option.propToSetPropInfo.DefID))
                                    {
                                        criteriaMet = true;
                                        PropertyValue newProperty = createPropertyValue(option.propToSetPropInfo, false);
                                        propertyValues.Add(-1, newProperty);
                                        addToGlobalPropertiesList(option.propToSetPropInfo);
                                        break;
                                    }
                                    else {
                                        logger.LogToFile($"The following property: {option.propToSetPropInfo.PropertyName} is already set to {propertiesSet[option.propToSetPropInfo.DefID]}. There is a duplicate setting trying to set this property to {option.propToSetPropInfo.StringValue}. Maintaining original value. ", "Info");
                                    }
                                }


                            }
                        }
                    }
                }
            }
        
        }

        public void ProcessConcatenatedProperties(PropertyValues propertyValues)
        {
            List<PropertyInfo> concatenatedProps = config.concactenatedProperties;
            foreach (PropertyInfo property in concatenatedProps)
            {
                try
                {
                    string NewValue = "";
                    string[] SectionsToConcat = property.StringValue.Split('|');
                    foreach (string section in SectionsToConcat)
                    {
                        string[] sectioninfo = section.Split(':');
                        if (sectioninfo[0] == "Text" || sectioninfo[0] == "FileInfo")
                        {
                            if (NewValue == "")
                            {
                                NewValue = NewValue + sectioninfo[1];
                            }
                            else {
                                NewValue = NewValue + property.Delimiter+ sectioninfo[1];
                            }
                        }
                        if (sectioninfo[0] == "Property")
                        {
                            int defID;
                            bool success = int.TryParse(sectioninfo[1], out defID);
                            if (!success)
                            {
                                defID = handler.vault.PropertyDefOperations.GetPropertyDefIDByAlias(sectioninfo[1]);
                            }
                            if (propertiesSet.ContainsKey(defID))
                            {
                                if (NewValue == "")
                                {
                                    NewValue = NewValue + propertiesSet[defID];
                                }
                                else
                                {
                                    NewValue = NewValue + property.Delimiter + propertiesSet[defID];
                                }
                            }
                        }

                    }
                    PropertyInfo concatProp = property.clone();
                    concatProp.StringValue = NewValue;
                    PropertyValue propval = new PropertyValue();
                    propval = createPropertyValue(concatProp, false);
                    propertyValues.Add(-1, propval);
                    addToGlobalPropertiesList(concatProp);
                }
                catch (Exception ex)
                {
                    missingRequiredProp = false;
                    throw new Exception($"The Following default propperty is invalid: { property.PropertyName }. Setting required Prop missing = true" + ex.Message);
                }
            }
        }
        public PropertyValue createPropertyValue(PropertyInfo propertyInfo, bool isConcatProp)
        {
            PropertyValue propval = new PropertyValue();
            try
            {
                
                propval.PropertyDef = propertyInfo.DefID;
                string PropertyStrValue = propertyInfo.StringValue;

                if (propertyInfo.dataType == MFDataType.MFDatatypeLookup)
                {
                    propval.TypedValue.SetValue(MFDataType.MFDatatypeLookup, propertyInfo.itemID);
                }
                if (propertyInfo.dataType == MFDataType.MFDatatypeMultiSelectLookup)
                {
                    ArrayList ListOfDefID = new ArrayList();
                    ListOfDefID.Add(propertyInfo.itemID);
                    propval.TypedValue.SetValue(MFDataType.MFDatatypeMultiSelectLookup, ListOfDefID.ToArray());
                }
                if (propertyInfo.dataType == MFDataType.MFDatatypeBoolean)
                {
                    if (PropertyStrValue.ToLower() == "false")
                    {
                        propval.TypedValue.SetValue(MFDataType.MFDatatypeBoolean, false);
                    }
                    else
                    {
                        propval.TypedValue.SetValue(MFDataType.MFDatatypeBoolean, true);
                    }
                }
                if (propertyInfo.dataType == MFDataType.MFDatatypeDate)
                {
                    PropertyStrValue = PropertyStrValue.Replace("{modified}", File.GetLastWriteTime(CurrentFilePath).ToString("MM/dd/yyyy"));
                    propval.TypedValue.SetValue(MFDataType.MFDatatypeDate, PropertyStrValue);
                }
                if (propertyInfo.dataType == MFDataType.MFDatatypeText ||propertyInfo.dataType== MFDataType.MFDatatypeMultiLineText)
                {
                    PropertyStrValue = PropertyStrValue.Replace("{modified}", File.GetLastWriteTime(CurrentFilePath).ToString("MM/dd/yyyy"));
                    PropertyStrValue = PropertyStrValue.Replace("{filename}", Path.GetFileName(CurrentFilePath));
                    PropertyStrValue = PropertyStrValue.Replace("{path}", CurrentFilePath.Replace(config.ApplicationSettings.StartFolderLocation, ""));
                    propval.TypedValue.SetValue(propertyInfo.dataType, PropertyStrValue);
                }
            }
            catch (Exception ex)
            {
                logger.LogToFile($"error creating PropertyValue: {propertyInfo.PropertyName} " + ex.Message, "Info");
                throw new Exception($"error creating PropertyValue: { propertyInfo.PropertyName } " + ex.Message);
            }

            return propval;
        }

        public void addToGlobalPropertiesList(PropertyInfo propertyInfo)
        {
            string PropertyStrValue = propertyInfo.StringValue;
            PropertyStrValue = PropertyStrValue.Replace("{modified}", File.GetLastWriteTime(CurrentFilePath).ToString("MM/dd/yyyy"));
            PropertyStrValue = PropertyStrValue.Replace("{filename}", Path.GetFileName(CurrentFilePath));
            PropertyStrValue = PropertyStrValue.Replace("{path}", CurrentFilePath.Replace(config.ApplicationSettings.StartFolderLocation, ""));
            propertiesSet.Add(propertyInfo.DefID, PropertyStrValue);
        }

        public void CreateFileInMFiles(PropertyValues propertyValues)
        {
            try
            {
                // Define the source files to add.
                SourceObjectFiles sourceFiles = new SourceObjectFiles();

                int objectTypeID = (int)MFBuiltInObjectType.MFBuiltInObjectTypeDocument;
                // Add one file.
                SourceObjectFile currentWorkingFile = new MFilesAPI.SourceObjectFile();
                currentWorkingFile.SourceFilePath = CurrentFilePath;
                currentWorkingFile.Title = "My text file";
                currentWorkingFile.Extension = Path.GetExtension(CurrentFilePath).TrimStart('.'); ;

                sourceFiles.Add(-1, currentWorkingFile);        

                ObjectVersionAndProperties oObjectVersionAndProperties = handler.vault.ObjectOperations.CreateNewObjectEx(objectTypeID, propertyValues, sourceFiles, true, true, null);
                //======================================END TEST==========================================================================               
                logger.LogToFile($"Sucessfully Added the following file in Mfiles:{CurrentFilePath} ", "Info");
                logger.LogToFile($"Properties Settings:" , "Info");
                foreach (KeyValuePair<int, string> pair in propertiesSet)
                {
                    logger.LogToFile($"{pair.Key},{pair.Value} " , "Info");
                }

                if (deleteFiles == true)
                {
                    CleanUpFileAndDirectory(CurrentFilePath);
                }

                // ObjectVersionAndProperties oObjectVersionAndProperties = handler.vault.ObjectOperations.CreateNewObjectEx(objectTypeID, propertyValues, sourceFiles, false, true, null);
            }
            catch (Exception ex)
            {
                logger.LogToFile($"ERROR:There was an error adding the following file in Mfiles:{CurrentFilePath} " + ex.Message, "Info");
                logger.LogToFile($"Properties Settings:" , "Info");
                foreach (KeyValuePair<int, string> pair in propertiesSet)
                {
                    logger.LogToFile($"{pair.Key},{pair.Value} ", "Info");
                }
                logger.LogToSkippedFile(CurrentFilePath,"debug");
            }
        }

        public void CleanUpFileAndDirectory(string CurrentFilePath)
        {
            string CurrentDirectory = Path.GetDirectoryName(CurrentFilePath);

            try
            {
                logger.LogToFile($"Deleting the following document {CurrentFilePath}", "Info");
                File.Delete(CurrentFilePath);
                logger.LogToFile($"Deleted sucessfully!", "Info");
            }
            catch (Exception ex)
            {
                logger.LogToFile($"ERROR: There was an error while deleting the document: {ex.Message}", "Info");
            }

            try { 
                string[] directories = Directory.GetDirectories(CurrentDirectory);
                string[] filesinDir = Directory.GetFiles(CurrentDirectory);
                if (directories.Count() == 0 && filesinDir.Count() == 0)
                {
                    logger.LogToFile($"Directory is empty, Deleting the following directory {CurrentDirectory}", "Info");
                    Directory.Delete(CurrentDirectory);
                }
                else {
                    logger.LogToFile($"Directory is not empty, continue processing subfiles.", "Info");
                }
            }
            catch (Exception ex)
            {
                logger.LogToFile($"ERROR: There was an error while deleting directory: {ex.Message}", "Info");
            }
        }

    }
}
