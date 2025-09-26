using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LamisPlusModulesInstaller
{
    public class ModuleUploadResponse
    {
        public int? Id { get; set; } // changed from nullable string to int
        public string Name { get; set; } = string.Empty;
        public string? BasePackage { get; set; }
        public string? Description { get; set; }
        public string Version { get; set; } = string.Empty;
        public string Artifact { get; set; } = string.Empty;
        public bool Active { get; set; }
        public bool New { get; set; }

        public DateTime? BuildTime { get; set; }
        public string? UmdLocation { get; set; }
        public object? ModuleMap { get; set; }
        public bool? InError { get; set; }
        public bool? InstallOnBoot { get; set; }
        public int Priority { get; set; }
        public List<WebModule> WebModules { get; set; } = new();
        public string? Type { get; set; }
        public string? Message { get; set; }
        public string? GitHubLink { get; set; }
        public string? LatestVersion { get; set; }
        public string? DownloadUrl { get; set; }
        public DateTime? LastSuccessfulUpdateCheck { get; set; }
        public List<Permission> Permissions { get; set; } = new();
    }

    public class WebModule
    {
        public int? Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Breadcrumb { get; set; } = string.Empty;
        public int Position { get; set; }
        public string Type { get; set; } = string.Empty;
        public List<string> Authorities { get; set; } = new();
        public bool New { get; set; }
    }

    public class Permission
    {
        public string? Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ModuleName { get; set; } = string.Empty;
    }

    public class ModuleInstallResponse
    {
        public string? Type { get; set; }   // "SUCCESS" or "ERROR"
        public string? Message { get; set; }
        public ModuleUploadResponse? Module { get; set; }
    }
}
