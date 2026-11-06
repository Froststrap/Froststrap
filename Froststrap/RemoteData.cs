// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using System.Security;
using System.Security.Cryptography;

namespace Froststrap
{
    internal class RemoteDataManager : JsonManager<RemoteDataBase>
    {
        public override string ClassName => nameof(RemoteDataManager);

        public override string FileLocation => Path.Combine(Paths.Base, "Data.json");

        public GenericTriState LoadedState = GenericTriState.Unknown;

        public event EventHandler DataLoaded = null!;

        private const int MaxSignatureLength = 80;
        private string SignatureFileLocation => $"{FileLocation}.p256.sig";
        private const string ConfigPublicKeyBase64 = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEJdlC7QtmASE6ErcMxYrF/zhB6Yur5v+as5eEic5sTawX1weypXzLxZiyqltXS+naupU498V622IGLQAuG/xHTQ==";
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
            if (App.Settings.Prop.ForceLocalData)
            {
                App.Logger.Info("Force loading local data");
                LoadedState = LoadLocalVerifiedData() ? GenericTriState.Successful : GenericTriState.Failed;
            }
            else
            {
                try
                {
                    Uri remoteDataUri = new(App.ProjectRemoteDataLink);
                    Uri remoteSigUri = new($"{App.ProjectRemoteDataLink}.p256.sig");

                    App.Logger.Info("Fetching remote Data.json and signature...");

                    Task<byte[]> dataTask = App.HttpClient.GetByteArrayAsync(remoteDataUri);
                    Task<byte[]> sigTask = App.HttpClient.GetByteArrayAsync(remoteSigUri);
                    await Task.WhenAll(dataTask, sigTask);
                    byte[] dataBytes = await dataTask;
                    byte[] sigBytes = await sigTask;

                    if (!VerifyEcdsa(dataBytes, sigBytes, ConfigPublicKey))
                    {
                        throw new SecurityException("Remote Data.json signature verification failed!");
                    }

                    App.Logger.Info("Remote Data.json signature verified successfully.");

                    Prop = JsonHelper.Deserialize<RemoteDataBase>(dataBytes)
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
                App.Logger.Info("Saved verified Data.json and Data.json.p256.sig to local cache.");
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

                if (!VerifyEcdsa(localDataBytes, localSigBytes, ConfigPublicKey))
                {
                    App.Logger.Error("LOCAL DISK CACHE TAMPERED! Signature check failed for local Data.json.");

                    File.Delete(FileLocation);
                    File.Delete(SignatureFileLocation);

                    this.Load(false);
                    return false;
                }

                Prop = JsonHelper.Deserialize<RemoteDataBase>(localDataBytes)!;
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

        private static bool VerifyEcdsa(byte[] data, byte[] signature, byte[] publicKey)
        {
            if (data == null || signature == null || publicKey == null)
                return false;

            if (signature.Length == 0 || signature.Length > MaxSignatureLength)
                return false;

            try
            {
                using var ecdsa = ECDsa.Create();
                ecdsa.ImportSubjectPublicKeyInfo(publicKey, out _);
                return ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256,
                    DSASignatureFormat.Rfc3279DerSequence);
            }
            catch
            {
                return false;
            }
        }
    }
}
