using System.Linq;
using Raven.Client.ServerWide;
using Sparrow.Json.Parsing;

namespace Raven.Client.Documents.Operations.Backups
{
    public abstract class RestoreBackupConfigurationBase : IDynamicJson
    {
        public string DatabaseName { get; set; }

        public string LastFileNameToRestore { get; set; }

        public string DataDirectory { get; set; }

        public string EncryptionKey { get; set; }

        public bool DisableOngoingTasks { get; set; }

        public bool SkipIndexes { get; set; }

        protected abstract RestoreType Type { get; }

        public ShardRestoreSetting[] ShardRestoreSettings { get; set; }

        public BackupEncryptionSettings BackupEncryptionSettings { get; set; }

        protected RestoreBackupConfigurationBase(RestoreBackupConfigurationBase other)
        {
            DatabaseName = other.DatabaseName;
            LastFileNameToRestore = other.LastFileNameToRestore;
            DataDirectory = other.DataDirectory;
            EncryptionKey = other.EncryptionKey;
            DisableOngoingTasks = other.DisableOngoingTasks;
            SkipIndexes = other.SkipIndexes;
            ShardRestoreSettings = other.ShardRestoreSettings;
            BackupEncryptionSettings = other.BackupEncryptionSettings;
        }

        protected RestoreBackupConfigurationBase()
        {
        }
        public abstract RestoreBackupConfigurationBase Clone();

        public virtual DynamicJsonValue ToJson()
        {
            return new DynamicJsonValue
            {
                [nameof(DatabaseName)] = DatabaseName,
                [nameof(LastFileNameToRestore)] = LastFileNameToRestore,
                [nameof(DataDirectory)] = DataDirectory,
                [nameof(EncryptionKey)] = EncryptionKey,
                [nameof(DisableOngoingTasks)] = DisableOngoingTasks,
                [nameof(SkipIndexes)] = SkipIndexes,
                [nameof(BackupEncryptionSettings)] = BackupEncryptionSettings,
                [nameof(Type)] = Type,
                [nameof(ShardRestoreSettings)] = new DynamicJsonArray(ShardRestoreSettings.Select(x => x.ToJson())),
            };
        }
    }

    public class ShardRestoreSetting : IDynamicJson
    {
        public int ShardNumber { get; set; }
        public string NodeTag { get; set; }
        public string BackupPath { get; set; }

        public DynamicJsonValue ToJson()
        {
            return new DynamicJsonValue
            {
                [nameof(ShardNumber)] = ShardNumber,
                [nameof(NodeTag)] = NodeTag,
                [nameof(BackupPath)] = BackupPath
            };
        }
    }

    public class RestoreBackupConfiguration : RestoreBackupConfigurationBase
    {
        public string BackupLocation { get; set; }

        protected override RestoreType Type => RestoreType.Local;

        public RestoreBackupConfiguration()
        {
        }

        protected RestoreBackupConfiguration(RestoreBackupConfiguration other) : base(other)
        {
            BackupLocation = other.BackupLocation;
        }

        public override DynamicJsonValue ToJson()
        {
            var json = base.ToJson();
            json[nameof(BackupLocation)] = BackupLocation;
            return json;
        }

        public override RestoreBackupConfigurationBase Clone()
        {
            return new RestoreBackupConfiguration(this);
        }
    }

    public class RestoreFromS3Configuration : RestoreBackupConfigurationBase
    {
        public S3Settings Settings { get; set; } = new S3Settings();

        protected override RestoreType Type => RestoreType.S3;

        public RestoreFromS3Configuration()
        {
        }

        protected RestoreFromS3Configuration(RestoreFromS3Configuration other) : base(other)
        {
            Settings = other.Settings;
        }

        public override DynamicJsonValue ToJson()
        {
            var json = base.ToJson();
            json[nameof(Settings)] = Settings;
            return json;
        }

        public override RestoreBackupConfigurationBase Clone()
        {
            return new RestoreFromS3Configuration(this);
        }

    }

    public class RestoreFromAzureConfiguration : RestoreBackupConfigurationBase
    {
        public AzureSettings Settings { get; set; } = new AzureSettings();

        protected override RestoreType Type => RestoreType.Azure;

        public RestoreFromAzureConfiguration()
        {
        }

        protected RestoreFromAzureConfiguration(RestoreFromAzureConfiguration other) : base(other)
        {
            Settings = other.Settings;
        }

        public override DynamicJsonValue ToJson()
        {
            var json = base.ToJson();
            json[nameof(Settings)] = Settings;
            return json;
        }

        public override RestoreBackupConfigurationBase Clone()
        {
            return new RestoreFromAzureConfiguration(this);
        }
    }  
    
    public class RestoreFromGoogleCloudConfiguration : RestoreBackupConfigurationBase
    {
        public GoogleCloudSettings Settings { get; set; } = new GoogleCloudSettings();

        protected override RestoreType Type => RestoreType.GoogleCloud;

        public RestoreFromGoogleCloudConfiguration()
        {
        }

        protected RestoreFromGoogleCloudConfiguration(RestoreFromGoogleCloudConfiguration other) : base(other)
        {
            Settings = other.Settings;
        }

        public override DynamicJsonValue ToJson()
        {
            var json = base.ToJson();
            json[nameof(Settings)] = Settings;
            return json;
        }

        public override RestoreBackupConfigurationBase Clone()
        {
            return new RestoreFromGoogleCloudConfiguration(this);
        }

    }

    public enum RestoreType
    {
        Local,
        S3,
        Azure,
        GoogleCloud
    }
}
