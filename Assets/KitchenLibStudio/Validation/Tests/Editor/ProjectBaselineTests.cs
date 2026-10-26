using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace KitchenLib.Studio.Tests.Editor
{
    public sealed class ProjectBaselineTests
    {
        [Test]
        public void UnityVersionIsPinned()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string versionFile = Path.Combine(projectRoot, "ProjectSettings", "ProjectVersion.txt");
            string contents = File.ReadAllText(versionFile);

            StringAssert.Contains("m_EditorVersion: 2020.3.48f1", contents);
            StringAssert.Contains("m_EditorVersionWithRevision: 2020.3.48f1 (b805b124c6b7)", contents);
        }
    }
}
