using System.Collections.Generic;
using System.Runtime.Serialization;

namespace OneDriveClient.Model
{
    public class Application
    {
        public string id { get; set; }
        public string displayName { get; set; }
    }

    public class User
    {
        public string id { get; set; }
        public string displayName { get; set; }
    }

    public class CreatedBy
    {
        public Application application { get; set; }
        public User user { get; set; }
    }

    public class Folder
    {
        public int childCount { get; set; }
    }

    public class Application2
    {
        public string id { get; set; }
        public string displayName { get; set; }
    }

    public class User2
    {
        public string id { get; set; }
        public string displayName { get; set; }
    }

    public class LastModifiedBy
    {
        public Application2 application { get; set; }
        public User2 user { get; set; }
    }

    public class ParentReference
    {
        public string driveId { get; set; }
        public string id { get; set; }
        public string path { get; set; }
    }

    public class Package
    {
        public string type { get; set; }
    }

    public class File
    {
    }

    public class Value
    {
        public CreatedBy createdBy { get; set; }
        public string createdDateTime { get; set; }
        public string cTag { get; set; }
        public string eTag { get; set; }
        public Folder folder { get; set; }
        public string id { get; set; }
        public LastModifiedBy lastModifiedBy { get; set; }
        public string lastModifiedDateTime { get; set; }
        public string name { get; set; }
        public ParentReference parentReference { get; set; }
        public long size { get; set; }
        public string webUrl { get; set; }
        public Package package { get; set; }
        [DataMember(Name = "@microsoft.graph.downloadUrl")]
        public string downloadUrl { get; set; }
        public File file { get; set; }
    }

    public class RootObject
    {
        [DataMember(Name = "@odata.context")]
        public string context { get; set; }
        public List<Value> value { get; set; }
    }
}