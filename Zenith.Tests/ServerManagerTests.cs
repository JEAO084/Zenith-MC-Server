using System;
using System.IO;
using Xunit;
using Zenith.Core.Services;

namespace Zenith.Tests
{
    public class ServerManagerTests : IDisposable
    {
        private readonly string _tempFolder;
        private readonly ServerManagerService _serverManager;

        public ServerManagerTests()
        {
            _tempFolder = Path.Combine(Path.GetTempPath(), "ZenithManagerTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempFolder);
            _serverManager = new ServerManagerService(_tempFolder);
        }

        [Fact]
        public void InitialProfiles_ContainsDefaultProfile()
        {
            var profiles = _serverManager.GetProfiles();
            Assert.NotEmpty(profiles);
            Assert.NotNull(_serverManager.ActiveProfile);
        }

        [Fact]
        public void CreateProfile_AddsAndSelectsProfile()
        {
            var newProfile = _serverManager.CreateProfile("Survival 1.20", "Paper", "1.20.4", 8);

            Assert.NotNull(newProfile);
            Assert.Equal("Survival 1.20", newProfile.Name);
            Assert.Equal(8, newProfile.AllocatedRamGb);
            Assert.Equal(newProfile.Id, _serverManager.ActiveProfile?.Id);
            Assert.True(Directory.Exists(newProfile.DirectoryPath));
        }

        [Fact]
        public void DeleteProfile_RemovesProfile()
        {
            var profile = _serverManager.CreateProfile("To Delete", "Vanilla", "1.20.4", 2);
            string id = profile.Id;

            _serverManager.DeleteProfile(id, true);
            var profiles = _serverManager.GetProfiles();

            Assert.DoesNotContain(profiles, p => p.Id == id);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempFolder))
            {
                try
                {
                    Directory.Delete(_tempFolder, true);
                }
                catch { }
            }
        }
    }
}
