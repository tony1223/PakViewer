// XmlCracker - UI 適配層
// 實際 XML 加解密邏輯由 Lin.Helper.Core.Xml.XmlCracker 提供

using System.Text;
using CoreXmlCracker = Lin.Helper.Core.Xml.XmlCracker;

namespace PakViewer.Utility
{
  /// <summary>
  /// XML 加解密 (UI 適配層)
  /// </summary>
  public static class XmlCracker
  {
    /// <summary>
    /// 檢查資料是否為加密的 XML
    /// </summary>
    public static bool IsEncrypted(byte[] data) => CoreXmlCracker.IsEncrypted(data);

    /// <summary>
    /// 檢查資料是否為解密的 XML
    /// </summary>
    public static bool IsDecryptedXml(byte[] data) => CoreXmlCracker.IsDecryptedXml(data);

    /// <summary>
    /// 從 XML 資料中解析 encoding 聲明，回傳對應的 Encoding
    /// </summary>
    public static Encoding GetXmlEncoding(byte[] data, string fallbackByFileName = null)
      => CoreXmlCracker.GetXmlEncoding(data, fallbackByFileName);

    /// <summary>
    /// 解密 XML 資料
    /// </summary>
    public static byte[] Decrypt(byte[] bytes) => CoreXmlCracker.Decrypt(bytes);

    /// <summary>
    /// 加密 XML 資料
    /// </summary>
    public static byte[] Encrypt(byte[] bytes) => CoreXmlCracker.Encrypt(bytes);
  }
}
