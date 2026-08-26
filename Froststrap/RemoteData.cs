using System;
using System.IO;
using System.Net.Http;
using System.Security;
using System.Text.Json;
using System.Threading.Tasks;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace Froststrap
{
    public class RemoteDataManager : JsonManager<RemoteDataBase>
    {
        public override string ClassName => nameof(RemoteDataManager);

        public override string FileLocation => Path.Combine(Paths.Base, "Data.json");

        public GenericTriState LoadedState = GenericTriState.Unknown;

        public event EventHandler DataLoaded = null!;

        private const int Ed25519PublicKeyLength = 32;
        private const int Ed25519SignatureLength = 64;

        private const string ConfigPublicKeyBase64 = "frqqb5rEBhsU5pMkPQDQYwM3FyEmJWWIQWsVKztwzrI="; // no this isn't my private key
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
                    Handler(this, EventArgs.Empty); // data loading most likely failed but we still have the default/local config
                    break;
            }
        }

        public async Task WaitUntilDataFetched()
        {
            const int delay = 100;
            const int maxTries = 30; // 3 seconds
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
                this.Load(false);

                LoadedState = GenericTriState.Successful; // we treat it as successful to simulate the production data
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

                    if (sigBytes.Length != Ed25519SignatureLength)
                    {
                        throw new SecurityException($"Invalid signature length ({sigBytes.Length} bytes). Expected {Ed25519SignatureLength} bytes.");
                    }

                    if (!VerifyEd25519(dataBytes, sigBytes, ConfigPublicKey))
                    {
                        throw new SecurityException("Cryptographic verification failed: Data.json signature is invalid or tampered with!");
                    }

                    App.Logger.Info("Data.json signature verified successfully.");

                    Prop = JsonSerializer.Deserialize<RemoteDataBase>(dataBytes)
                           ?? throw new JsonException("Deserialized remote data was null.");

                    LoadedState = GenericTriState.Successful;
                    App.Logger.Info("Remote data loaded");
                }
                catch (Exception ex)
                {
                    // Network failed OR signature verification failed
                    // Keep existing local Data.json intact and fall back to it
                    App.Logger.Error($"Could not load remote data: {ex.Message}");
                    App.Logger.Info("Loading local data instead");

                    this.Load(false);
                    LoadedState = GenericTriState.Failed;
                }
            }

            DataLoaded?.Invoke(this, EventArgs.Empty);

            // Only overwrite local cache if remote fetch & verification succeeded
            if (LoadedState == GenericTriState.Successful)
                this.Save();

            App.Logger.Info($"Loading finished with status: {LoadedState}");
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
