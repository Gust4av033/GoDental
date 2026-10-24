using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using GoDental.Application.DTOs;

namespace GoDental.Infrastructure.Security;

[SupportedOSPlatform("windows")]
internal static class WindowsSecretDirectory
{
    private static readonly SecurityIdentifier SystemSid = new(WellKnownSidType.LocalSystemSid, null);
    private static readonly SecurityIdentifier AdminSid = new(WellKnownSidType.BuiltinAdministratorsSid, null);
    private static readonly SecurityIdentifier InstallerSid = new("S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464");

    internal static SecurityIdentifier CurrentSid(string expectedSid)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var actual = identity.User ?? throw new SecretStoreException(SecretStoreFailure.Unavailable);
        if (actual.Value != expectedSid) throw new SecretStoreException(SecretStoreFailure.Unavailable);
        return actual;
    }

    internal static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal) ||
            path.IndexOf(':', 2) >= 0 || path.Contains("..", StringComparison.Ordinal))
            throw new SecretStoreException(SecretStoreFailure.Unavailable);
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var drive = new DriveInfo(Path.GetPathRoot(full)!);
        if (drive.DriveType != DriveType.Fixed || drive.DriveFormat != "NTFS" || full == Path.GetPathRoot(full))
            throw new SecretStoreException(SecretStoreFailure.Unavailable);
        return full;
    }

    internal static DirectorySecurity PrivateAcl(SecurityIdentifier sid)
    {
        var security = new DirectorySecurity();
        security.SetOwner(sid);
        security.SetAccessRuleProtection(true, false);
        foreach (var principal in new[] { sid, SystemSid }.Distinct())
            security.AddAccessRule(new FileSystemAccessRule(principal, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None, AccessControlType.Allow));
        return security;
    }

    internal static void SetFileOwner(string path, SecurityIdentifier sid)
    {
        // Un proceso elevado puede crear archivos con Administradores como propietario por defecto.
        var security = new FileSecurity();
        security.SetOwner(sid);
        new FileInfo(path).SetAccessControl(security);
    }

    internal static void Validate(string root, SecurityIdentifier sid)
    {
        var directory = new DirectoryInfo(root);
        if (!directory.Exists) throw new SecretStoreException(SecretStoreFailure.Unavailable);
        for (DirectoryInfo? current = directory; current is not null; current = current.Parent)
        {
            if ((current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new SecretStoreException(SecretStoreFailure.Unavailable);
            // Un tercero que pueda eliminar el hijo puede sustituir la raíz aunque su ACL sea privada.
            var acl = current.GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner);
            if (current.FullName == directory.FullName)
            {
                ValidatePrivate(acl, sid, directory: true);
                continue;
            }
            var owner = acl.GetOwner(typeof(SecurityIdentifier));
            if (owner != sid && owner != SystemSid && owner != AdminSid && owner != InstallerSid)
                throw new SecretStoreException(SecretStoreFailure.Unavailable);
            foreach (FileSystemAccessRule rule in acl.GetAccessRules(true, true, typeof(SecurityIdentifier)))
                if (rule.AccessControlType == AccessControlType.Allow &&
                    (rule.PropagationFlags & PropagationFlags.InheritOnly) == 0 &&
                    rule.IdentityReference != sid && rule.IdentityReference != SystemSid && rule.IdentityReference != AdminSid &&
                    (rule.FileSystemRights & (FileSystemRights.DeleteSubdirectoriesAndFiles | FileSystemRights.Delete |
                        FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership)) != 0)
                    throw new SecretStoreException(SecretStoreFailure.Unavailable);
        }
    }

    internal static void ValidateFile(string path, SecurityIdentifier sid)
    {
        var file = new FileInfo(path);
        if ((file.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
            throw new SecretStoreException(SecretStoreFailure.Unavailable);
        ValidatePrivate(file.GetAccessControl(AccessControlSections.Access | AccessControlSections.Owner), sid, directory: false);
    }

    private static void ValidatePrivate(FileSystemSecurity acl, SecurityIdentifier sid, bool directory)
    {
        var owner = acl.GetOwner(typeof(SecurityIdentifier));
        if ((owner != sid && owner != SystemSid) || (directory && !acl.AreAccessRulesProtected))
            throw new SecretStoreException(SecretStoreFailure.Unavailable);
        var serviceAllowed = false;
        foreach (FileSystemAccessRule rule in acl.GetAccessRules(true, true, typeof(SecurityIdentifier)))
        {
            if (rule.AccessControlType != AccessControlType.Allow) continue;
            if (rule.IdentityReference != sid && rule.IdentityReference != SystemSid)
                throw new SecretStoreException(SecretStoreFailure.Unavailable);
            if (rule.IdentityReference == sid && (rule.FileSystemRights & FileSystemRights.FullControl) == FileSystemRights.FullControl &&
                (rule.PropagationFlags & PropagationFlags.InheritOnly) == 0 &&
                (!directory || (rule.InheritanceFlags & (InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit)) ==
                    (InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit))) serviceAllowed = true;
        }
        if (!serviceAllowed) throw new SecretStoreException(SecretStoreFailure.Unavailable);
    }
}
