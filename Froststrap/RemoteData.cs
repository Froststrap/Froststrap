using System.Security;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace Froststrap
{
    internal class RemoteDataManager : JsonManager<RemoteDataBase>
    {
        public override string ClassName => nameof(RemoteDataManager);

        public override string FileLocation => Path.Combine(Paths.Base, "Data.json");
        private string SignatureFileLocation => $"{FileLocation}.sig";

        public GenericTriState LoadedState = GenericTriState.Unknown;

        public event EventHandler DataLoaded = null!;

        private const int Ed25519PublicKeyLength = 32;
        private const int Ed25519SignatureLength = 64;

        private const string ConfigPublicKeyBase64 = "frqqb5rEBhsU5pMkPQDQYwM3FyEmJWWIQWsVKztwzrI=";
        private static readonly byte[] ConfigPublicKey = Convert.FromBase64String(ConfigPublicKeyBase64);

        public void Subscribe(EventHandler Handler)
        {
            switch (LoadedState)
            {
                case GenericTriState.Unknown:
                    DataLoaded += Handler;
                    break;
                case GenericTriState.Successful:
                    Handler(this, EventArgs.Empty);
                    break;
                default:
                    Handler(this, EventArgs.Empty);
                    break;
            }
        }

        public async Task WaitUntilDataFetched()
        {
            const int delay = 100;
            const int maxTries = 30;
            int tries = 0;

            while (LoadedState == GenericTriState.Unknown)
            {
                await Task.Delay(delay);
                tries++;

                if (tries >= maxTries)
                    break;
            }
        }

        public async Task LoadData()
        {
            if (App.Settings.Prop.ForceLocalData || App.LaunchSettings.WatcherFlag.Active)
            {
                App.Logger.Info("Force loading local data");
                LoadLocalVerifiedData();
                LoadedState = GenericTriState.Successful;
            }
            else
            {
                try
                {
                    Uri remoteDataUri = new(App.ProjectRemoteDataLink);
                    Uri remoteSigUri = new($"{App.ProjectRemoteDataLink}.sig");

                    App.Logger.Info("Fetching remote Data.json and signature...");

                    using var client = new HttpClient();
                    byte[] dataBytes = await client.GetByteArrayAsync(remoteDataUri);
                    byte[] sigBytes = await client.GetByteArrayAsync(remoteSigUri);

                    if (sigBytes.Length != Ed25519SignatureLength || !VerifyEd25519(dataBytes, sigBytes, ConfigPublicKey))
                    {
                        throw new SecurityException("Remote Data.json signature verification failed!");
                    }

                    App.Logger.Info("Remote Data.json signature verified successfully.");

                    Prop = JsonSerializer.Deserialize<RemoteDataBase>(dataBytes)
                           ?? throw new JsonException("Deserialized remote data was null.");

                    LoadedState = GenericTriState.Successful;
                    App.Logger.Info("Remote data loaded");

                    SaveLocalVerifiedData(dataBytes, sigBytes);
                }
                catch (Exception ex)
                {
                    App.Logger.Error($"Could not load remote data: {ex.Message}");
                    App.Logger.Info("Attempting to load verified local cache instead...");

                    bool localSuccess = LoadLocalVerifiedData();
                    LoadedState = localSuccess ? GenericTriState.Successful : GenericTriState.Failed;
                }
            }

            DataLoaded?.Invoke(this, EventArgs.Empty);
            App.Logger.Info($"Loading finished with status: {LoadedState}");
        }

        private void SaveLocalVerifiedData(byte[] dataBytes, byte[] sigBytes)
        {
            try
            {
                File.WriteAllBytes(FileLocation, dataBytes);
                File.WriteAllBytes(SignatureFileLocation, sigBytes);
                App.Logger.Info("Saved verified Data.json and Data.json.sig to local cache.");
            }
            catch (Exception ex)
            {
                App.Logger.Warn($"Failed to save local cache: {ex.Message}");
            }
        }

        private bool LoadLocalVerifiedData()
        {
            if (!File.Exists(FileLocation) || !File.Exists(SignatureFileLocation))
            {
                App.Logger.Info("No complete local cache found. Loading default local config.");
                this.Load(false);
                return false;
            }

            try
            {
                byte[] localDataBytes = File.ReadAllBytes(FileLocation);
                byte[] localSigBytes = File.ReadAllBytes(SignatureFileLocation);

                if (localSigBytes.Length != Ed25519SignatureLength || !VerifyEd25519(localDataBytes, localSigBytes, ConfigPublicKey))
                {
                    App.Logger.Error("LOCAL DISK CACHE TAMPERED! Signature check failed for local Data.json.");

                    File.Delete(FileLocation);
                    File.Delete(SignatureFileLocation);

                    this.Load(false);
                    return false;
                }

                Prop = JsonSerializer.Deserialize<RemoteDataBase>(localDataBytes)!;
                App.Logger.Info("Successfully verified and loaded local Data.json cache from disk.");
                return true;
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Error reading local cache: {ex.Message}");
                this.Load(false);
                return false;
            }
        }

        private static bool VerifyEd25519(byte[] data, byte[] signature, byte[] publicKey)
        {
            if (data == null || signature == null || publicKey == null)
                return false;

            if (signature.Length != Ed25519SignatureLength || publicKey.Length != Ed25519PublicKeyLength)
                return false;

            try
            {
                var pubKeyParams = new Ed25519PublicKeyParameters(publicKey, 0);
                var verifier = new Ed25519Signer();
                verifier.Init(forSigning: false, pubKeyParams);
                verifier.BlockUpdate(data, 0, data.Length);
                return verifier.VerifySignature(signature);
            }
            catch
            {
                return false;
            }
        }
    }
}
