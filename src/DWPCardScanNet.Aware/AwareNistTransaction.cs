using Aware.NistPack;
using DWPCardScanNet.Core.Processing;

namespace DWPCardScanNet.Aware
{
    /// <summary>
    /// A NIST transaction backed by the managed <see cref="NistPack"/> + <see cref="AwareVerification"/>.
    /// Replaces the VB6 AwTransCtrl COM control:
    /// AwReadVerification/AwNew/AwAddRecord/AwSet/AwSetImage/AwWrite.
    /// </summary>
    public sealed class AwareNistTransaction : INistTransaction
    {
        private readonly NistPack _nist = new NistPack();
        private readonly AwareVerification _verification = new AwareVerification();

        public void ReadVerification(string verificationFile) =>
            _verification.readVerification(verificationFile);

        public void NewTransaction(string transactionType) =>
            _nist.newTransaction(transactionType, _verification);

        public int AddRecord(int recordType) => _nist.addRecord(recordType);

        // VB6 AwSet's subfield/index argument was 0 (or omitted) at every call site.
        public void Set(string mnemonic, string data, int recordIndex) =>
            _nist.set(mnemonic, data, 0, recordIndex);

        public void SetImage(int recordType, int recordIndex, string inputFormat, byte[] image,
                             string storageFormat, float compression) =>
            _nist.setImage(recordType, recordIndex, inputFormat, image, storageFormat, compression);

        public void Write(string path) => _nist.writeTransaction(path);

        public void Dispose()
        {
            _nist?.Dispose();
            _verification?.Dispose();
        }
    }

    public sealed class AwareNistTransactionFactory : INistTransactionFactory
    {
        public INistTransaction Create() => new AwareNistTransaction();
    }
}
