using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MFilesAPI;

namespace MfilesObjectImporter
{
    public class MetadataConfig
    {
        
        public AppSettings ApplicationSettings { get; set; }
        public Dictionary<string, PropertyInfo> DefaultProperties { get; set; }
        public Dictionary<int, PropertyInfo> flps { get; set; }
       // public Dictionary<string, Dictionary<string, List<PropertyInfo>>> ConditionalProperties { get; set; }
        public List<ConditionalProperty> ConditionalProperties { get; set; }
        public Dictionary<string, Dictionary<string, List<PropertyInfo>>> ConditionalPropertiess { get; set; }
        public List<PropertyInfo> concactenatedProperties { get; set; }
        public Dictionary<int, Dictionary<string, PropertyInfo>> FolderLevelProperties { get; set; }  
    }

    public class PropertyInfo
    {
        public MFDataType dataType { get; set; }
        public int ParentValueListID { get; set; } = -1;
        public string PropertyName { get; set; }
        public int DefID { get; set; }
        public string StringValue { get; set; } = "";
        public int itemID { get; set; } = -1;
        //  public int folderLevel { get; set; } = -1;
        public string Alias { get; set; } = "";
        public string Delimiter { get; set; } = "";
        public bool isRequired { get; set; } 
        public bool updateOnTheFly { get; set; }
        public bool hasConditions { get; set; }
        public bool exactMatch { get; set; }


        public PropertyInfo clone()
        {
            PropertyInfo copy=  new PropertyInfo() ;
            copy.dataType = this.dataType;
            copy.ParentValueListID = this.ParentValueListID;
            copy.PropertyName = this.PropertyName;
            copy.DefID = this.DefID;
            copy.StringValue = this.StringValue;
            copy.itemID = this.itemID;
            copy.Alias = this.Alias;
            copy.Delimiter = this.Delimiter;
            copy.isRequired = this.isRequired;
            copy.updateOnTheFly = this.updateOnTheFly;
            copy.hasConditions = this.hasConditions;
            copy.exactMatch = this.exactMatch;
            return copy;        
        }

    }
    /// <summary>
    /// //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// </summary>
    public class AppSettings
    {
        public string username { get; set; }
        public string password { get; set; }
        public string protocol { get; set; }
        public int port { get; set; }
        public bool specificMFilesUser { get; set; }
        public string server { get; set; }
        public string vaultGUID { get; set; }
        public string StartFolderLocation { get; set; }
        public string LogFilePath { get; set; }
        public string ExcludedExtensions { get; set; }

    }

    public class MigrationConfigurations
    {
        public AppSettings AppSettings { get; set; }
        public List<Property> defaultPropertiesSettings { get; set; }
        public List<FolderSetting> FolderLevelSettings { get; set; }
        public List<ConditionalProperty> ConditionalPropertySettings { get; set; }
        public List<Property> concactenatedProperties { get; set; }

    }

    public class Property
    {
        public string idOrAlias { get; set; }
        public string value { get; set; }
        public string delimiter { get; set; }
    }

    public class FolderSetting
    {
        public int folderLevel { get; set; }
        public string idOrAlias { get; set; }
        public bool hasConditions { get; set; }
        public bool exactMatch { get; set; }
        public bool isRequired { get; set; }

        public List<Condition> ValueConditions { get; set; }

    }
    public class Conditions
    {
        public List<Condition> conditions { get; set; }
    }

    public class Condition
    {
        public string ifcontainsValue { get; set; }
        public string valueToSet { get; set; }
    }

    public class ConditionalProperty
    {
        public int ProcessingOrder { get; set; }
        public string parentProperty { get; set; }
        public List<ConditionalSettings> ParentChildConditions { get; set; }
    }

    public class ConditionalSettings
    {
        public string hasValue { get; set; }
        public string propertyToSet { get; set; }
        public bool ParentPropertyisVLOwner { get; set; }
        public string valueToSet { get; set; }
        public PropertyInfo propToSetPropInfo { get; set; }
        public List<Criteria> AditionalCriteria { get; set; }

    }

    public class Criteria
    {
        public List<FolderCriteria> FolderCriterias { get; set; }
        public List<PropertyCriteria> PropertyCriterias { get; set; }
        public string valueToSet { get; set; }
        public PropertyInfo propToSetPropInfo { get; set; }
    }

    public class FolderCriteria
    { 
        public int folderLevel { get; set; }
        public string ifcontainsValue { get; set; }
    }

    public class PropertyCriteria
    { 
        public string idOrAlias { get; set; }
        public string value { get; set; }
    }

}
