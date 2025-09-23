using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LamisPlusModulesInstaller
{
    public class ModuleUploadResponse
    {
        public string? Id { get; set; }
        public string Name { get; set; }
        public string? BasePackage { get; set; }
        public string? Description { get; set; }
        public string Version { get; set; }
        public string Artifact { get; set; }
        public bool Active { get; set; }
        public bool New { get; set; }
        public int Priority { get; set; } // added because of priority which API expects it (and it’s in the upload response JSON "priority":100)
    }

    public class ModuleInstallResponse
    {
        public string Type { get; set; }   // "SUCCESS" or "ERROR"
        public string Message { get; set; }
        public ModuleUploadResponse? Module { get; set; }
    }
}