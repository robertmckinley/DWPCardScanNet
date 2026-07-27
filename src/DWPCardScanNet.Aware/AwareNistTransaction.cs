using System;
using Aware.NistPack;
using DWPCardScanNet.Core.Processing;

namespace DWPCardScanNet.Aware
{
    /// <summary>
    /// A NIST transaction backed by the managed <see cref="NistPack"/> + <see cref="AwareVerification"/>.
    /// Replaces the VB6 AwTransCtrl COM control:
    /// AwReadVerification/AwNew/AwAddRecord/AwSet/AwSetImage/AwWrite.
    /// <para>
    /// Each Aware call is wrapped so a failure names the operation, mnemonic, TOT, or record involved.
    /// The raw Aware errors (e.g. ERR_MNEMONIC_NOT_FOUND) don't say which field failed; that context is
    /// what makes a bad verification-file/TOT/mnemonic combination diagnosable from the status report.
    /// </para>
    /// </summary>
    public sealed class AwareNistTransaction : INistTransaction
    {
        private readonly NistPack _nist = new NistPack();
        private readonly AwareVerification _verification = new AwareVerification();

        public void ReadVerification(string verificationFile)
        {
            try { _verification.readVerification(verificationFile); }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Failed to read verification file '" + verificationFile + "': " + ex.Message, ex);
            }
        }

        public void NewTransaction(string transactionType)
        {
            try { _nist.newTransaction(transactionType, _verification); }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Failed to start transaction type (TOT) '" + transactionType +
                    "' - check the TOT exists in the verification file: " + ex.Message, ex);
            }
        }

        public int AddRecord(int recordType) => _nist.addRecord(recordType);

        // VB6 AwSet's subfield/index argument was 0 (or omitted) at every call site.
        public void Set(string mnemonic, string data, int recordIndex)
        {
            try { _nist.set(mnemonic, data, 0, recordIndex); }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Failed to set mnemonic '" + mnemonic + "' (record index " + recordIndex +
                    ") - check it is defined for this transaction type in the verification file: " +
                    ex.Message, ex);
            }
        }

        public void SetImage(int recordType, int recordIndex, string inputFormat, byte[] image,
                             string storageFormat, float compression)
        {
            try { _nist.setImage(recordType, recordIndex, inputFormat, image, storageFormat, compression); }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Failed to set image on Type-" + recordType + " record index " + recordIndex +
                    ": " + ex.Message, ex);
            }
        }

        public void Write(string path)
        {
            try { _nist.writeTransaction(path); }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Failed to write NIST packet '" + path + "': " + ex.Message, ex);
            }
        }

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
