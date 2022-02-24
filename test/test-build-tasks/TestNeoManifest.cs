using System;
using Neo.BuildTasks;
using SimpleJSON;
using Xunit;

namespace build_tasks
{
    public class TestNeoManifest
    {
        [Fact]
        public void parse_sample_manifest()
        {
            var text = Utility.GetResource("apoc-manifest.json");
            var json = JSON.Parse(text) ?? throw new InvalidOperationException();
            var manifest = NeoManifest.FromJson(json);
            Assert.Equal("DevHawk.Contracts.ApocToken", manifest.Name);
            Assert.Equal(13, manifest.Methods.Count);
            Assert.Equal(1, manifest.Events.Count);
        }

        [Fact]
        public void test_add_extra()
        {
            var manifest = JSON.Parse(Utility.GetResource("registrar-manifest.json"));
            var extra = JSON.Parse(Utility.GetResource("sample-storage-schema.json"));
            test_manifest_extra(manifest, extra);
        }

        [Fact]
        public void test_add_extra_no_extra_in_source_manifest()
        {
            var manifest = JSON.Parse(Utility.GetResource("registrar-manifest-no-extra.json"));
            var extra = JSON.Parse(Utility.GetResource("sample-storage-schema.json"));
            test_manifest_extra(manifest, extra);
        }

        static void test_manifest_extra(JSONNode manifest, JSONNode extra)
        {
            NeoManifest.UpdateExtra(manifest, extra, nameof(TestNeoManifest));

            Assert.True(manifest["extra"][nameof(TestNeoManifest)].IsObject);

            var manifest2 = JSON.Parse(manifest.ToString());
            Assert.True(manifest2["extra"][nameof(TestNeoManifest)].IsObject);
            
            Assert.Equal(extra.ToString(), manifest2["extra"][nameof(TestNeoManifest)].ToString());
        }
    }
}
