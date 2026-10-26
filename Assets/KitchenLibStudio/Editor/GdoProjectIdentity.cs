using System;
using System.Text;
using System.Text.RegularExpressions;

namespace KitchenLib.Studio.Editor
{
    public static class GdoProjectIdentity
    {
        private static readonly Regex InvalidBundleCharacters = new Regex("[^a-z0-9]+", RegexOptions.Compiled);

        public static string Canonical(string projectName, string gdoName)
        {
            string normalizedProjectName = projectName == null ? null : projectName.Trim();
            string normalizedGdoName = gdoName == null ? null : gdoName.Trim();
            ValidateName(normalizedProjectName, nameof(projectName));
            ValidateName(normalizedGdoName, nameof(gdoName));
            return normalizedProjectName.ToLowerInvariant() + ":" + normalizedGdoName.ToLowerInvariant();
        }

        public static int RuntimeId(string projectName, string gdoName)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(Canonical(projectName, gdoName));
            unchecked
            {
                uint hash = 2166136261;
                foreach (byte value in bytes)
                    hash = (hash ^ value) * 16777619;

                int id = -(int)(hash & 0x7fffffff);
                return id == 0 ? -1 : id;
            }
        }

        public static string Canonical(string authorName, string projectName, string gdoName)
        {
            string normalizedAuthor = authorName == null ? null : authorName.Trim();
            ValidateName(normalizedAuthor, nameof(authorName));
            return normalizedAuthor.ToLowerInvariant() + ":" + Canonical(projectName, gdoName);
        }

        public static int RuntimeId(string authorName, string projectName, string gdoName)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(Canonical(authorName, projectName, gdoName));
            unchecked
            {
                uint hash = 2166136261;
                foreach (byte value in bytes)
                    hash = (hash ^ value) * 16777619;
                int id = -(int)(hash & 0x7fffffff);
                return id == 0 ? -1 : id;
            }
        }

        public static void ValidateName(string value, string argumentName)
        {
            string parameter = string.IsNullOrEmpty(argumentName) ? "value" : argumentName;
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Name must not be empty or whitespace.", parameter);
            if (value[value.Length - 1] == '.' || value[value.Length - 1] == ' ')
                throw new ArgumentException("Name must not end with a dot or space.", parameter);

            foreach (char character in value)
            {
                if (character == '/' || character == '\\' || char.IsControl(character) ||
                    character == '<' || character == '>' || character == ':' || character == '"' ||
                    character == '|' || character == '?' || character == '*')
                    throw new ArgumentException("Name contains a character that is invalid for Unity assets or Windows paths.", parameter);
            }

            string deviceCandidate = value.Split('.')[0].TrimEnd(' ', '.');
            if (IsWindowsDeviceName(deviceCandidate))
                throw new ArgumentException("Name must not be a reserved Windows device name.", parameter);
        }

        public static string BundleFileName(string projectName)
        {
            if (string.IsNullOrWhiteSpace(projectName))
                throw new ArgumentException("Project name must not be empty or whitespace.", nameof(projectName));
            string slug = InvalidBundleCharacters.Replace(projectName.Trim().ToLowerInvariant(), "-").Trim('-');
            return (string.IsNullOrEmpty(slug) ? "gdo-bundle" : slug) + ".assets";
        }

        private static bool IsWindowsDeviceName(string name)
        {
            if (string.Equals(name, "CON", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "PRN", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "AUX", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "NUL", StringComparison.OrdinalIgnoreCase))
                return true;

            if (name.Length == 4)
            {
                char prefix = char.ToUpperInvariant(name[0]);
                return (prefix == 'C' && char.ToUpperInvariant(name[1]) == 'O' && char.ToUpperInvariant(name[2]) == 'M' &&
                        name[3] >= '1' && name[3] <= '9') ||
                       (prefix == 'L' && char.ToUpperInvariant(name[1]) == 'P' && char.ToUpperInvariant(name[2]) == 'T' &&
                        name[3] >= '1' && name[3] <= '9');
            }

            return false;
        }
    }
}
