//
//  This code is the managed pdb reader interface for PERWAPI.
//  Written by John Gough, April 2007. 
//  Copyright(c) 2007-2008 John Gough, and Queensland University of Technology
//
using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Diagnostics.SymbolStore;

namespace QUT.Symbols {
  #region Utility Classes
  /// <summary>
  /// PInvoke hook to call COM CoCreateInstance.
  /// Plus any useful constants...
  /// </summary>
  internal static class OLE32 {
    internal const int hr_E_FAIL = unchecked((int)0x80004005);

    [DllImport("ole32.dll")]
    internal static extern int CoCreateInstance(
        [In] ref Guid rclsid,
        [In, MarshalAs(UnmanagedType.IUnknown)] object pUnkOuter,
        [In] uint dwClsContext,
        [In] ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out object ppv);
  }

  /// <summary>
  /// Some external guids, defined in cor.h, mainly
  /// </summary>
  internal static class XGuid {
    // This guid is the CLSID of the metadata dispenser object in mscoree.dll
    internal static Guid dispenserClassID = new Guid("{E5CB7A31-7512-11d2-89CE-0080C792E5D8}");
    // This guid is the IID of the IMetaDataDispenser interface defined in cor.h
    internal static Guid dispenserIID = new Guid("{809c652e-7396-11d2-9771-00a0c9b4d50c}");
    // This guid is the IID of the IMetaDataImport interface in cor.h
    internal static Guid importerIID = new Guid("{7DAC8207-D3AE-4c75-9B67-92801A497D44}");
    // This guid is the UUID for CorSymBinder_SxS, defined in corsym.h
    internal static Guid binderCLSID = new Guid("{0A29FF9E-7F9C-4437-8B11-F424491E3931}");
    internal static Guid binderIID = new Guid("{28AD3D43-B601-4d26-8A1B-25F9165AF9D7}");
    // This guid is the CLSID for CLSID_CorSymWriter_SxS, defined in corsym.h
    internal static Guid symWriterClassID = new Guid("0AE2DEB0-F901-478b-BB9F-881EE8066788");
    internal static Guid symWriterIID = new Guid("0B97726E-9E6D-4f05-9A26-424022093CAA");
  }

  [StructLayout(LayoutKind.Sequential)]
  public struct ImageDebugDirectory {
    private int Characteristics;
    private int TimeDateStamp;
    private short MajorVersion;
    private short MinorVersion;
    private int Type;
    private int SizeOfData;
    private int AddressOfRawData;
    private int PointerToRawData;
    // public override string ToString();
  }

  /// <summary>
  /// Some static helper methods
  /// </summary>
  internal static class Util {
    internal static void ComCheck(bool test, string message) {
      if (!test)
        throw new COMException(message);
    }

    internal static void ComCheckHR(int hr, string message) {
      if (hr == OLE32.hr_E_FAIL)
        throw new COMException(message);
    }

    internal static void ArgCheck(bool test, string message) {
      if (!test)
        throw new ArgumentException(message);
    }
  }

  #endregion // Utility Classes

}
