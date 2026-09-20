#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class BatchFbxImportServiceTests
{
    [Test]
    public void RebuildItems_PreservesObjAxisToggle()
    {
        BatchFbxImportSettings settings = ScriptableObject.CreateInstance<BatchFbxImportSettings>();
        try
        {
            var existing = new List<BatchFbxImportService.ImportItem>
            {
                new BatchFbxImportService.ImportItem
                {
                    SourceFbxPath = "D:/pack/unit/fbx/keep.obj",
                    Id2 = "KeepId",
                    ConvertZUpToYUp = true
                }
            };

            List<BatchFbxImportService.ImportItem> rebuilt =
                BatchFbxImportService.RebuildItems(existing, settings);

            Assert.That(rebuilt.Count, Is.EqualTo(1));
            Assert.That(rebuilt[0].ConvertZUpToYUp, Is.True);
            Assert.That(rebuilt[0].Id2, Is.EqualTo("KeepId"));
        }
        finally
        {
            Object.DestroyImmediate(settings);
        }
    }

    [Test]
    public void ToOrchestrationBinding_CopiesAxisForObj_IgnoresFbx()
    {
        PipelineSourceBinding obj = BatchFbxImportService.ToOrchestrationBinding(
            new BatchFbxImportService.ImportItem
            {
                SourceFbxPath = "D:/pack/unit/fbx/keep.obj",
                Id2 = "KeepId",
                ConvertZUpToYUp = true
            });
        Assert.That(obj, Is.Not.Null);
        Assert.That(obj.ConvertZUpToYUp, Is.True);
        Assert.That(obj.MaterialId, Is.EqualTo("KeepId"));

        PipelineSourceBinding fbx = BatchFbxImportService.ToOrchestrationBinding(
            new BatchFbxImportService.ImportItem
            {
                SourceFbxPath = "D:/pack/unit/fbx/keep.fbx",
                Id2 = "KeepId",
                ConvertZUpToYUp = true
            });
        Assert.That(fbx, Is.Not.Null);
        Assert.That(fbx.ConvertZUpToYUp, Is.False);
    }
}
#endif
