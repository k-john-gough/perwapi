//
//  This code is the managed pdb reader interface for PERWAPI.
//  Written by John Gough, April 2007. 
//  Copyright(c) 2007-2010 John Gough, and Queensland University of Technology
//
//  NOTES:
//  Q: Where does this code come from?
//  A: At the heart of the code is some COM interop that accesses the
//     facilities of mscoree.dll.  Unfortunately, the type library for
//     this component is incomplete, and does not cover the symbol store
//     functionality.  There is some MIDL, and the C++ header files
//     cor.h and corsym.h.  From here comes the guid values, for 
//     example, and critically the order of the interface methods.
//     The managed interfaces, such as ISymbolReader, are defined in 
//     [mscorlib]System.Diagnostics.SymbolStore.
//
//     This file provides managed wrappers for the unmanaged interfaces.
//     These wrappers implement the managed interfaces. 
//     Not all methods are implemented. Those that are not will throw 
//     NotImplementedException.
//
//     Some of the functionality currently not used by PERWAPI is left
//     unimplemented, and throws a NotImplementedException if called.
//
//  THANKS:
//  Wayne Kelly, whose SimpleWriter, in C++ pointed the way.
//  Adam Nathan, whose ".NET and COM" tells you more than you want to know
//  Microsoft's Iron Python and MDBG samples answered remaining questions
//

using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Diagnostics.SymbolStore;

namespace QUT.Symbols {
  public class SymbolReader : ISymbolReader {
    private ISymUnmanagedReader private_reader;

    /// <summary>
    /// Constructor for SymbolReader.
    /// Just creates a reference to the unmanaged reader
    /// from the COM world.  This Reader only implements
    /// the method(s) required by PERWAPI.
    /// </summary>
    /// <param name="filename"></param>
    public SymbolReader(string filename) {
      object ppb = null;
      object pUnk = null;
      object pBnd = null;
      IntPtr importer = IntPtr.Zero;
      object retVal = null;

      try {
        OLE32.CoCreateInstance(ref XGuid.dispenserClassID, null, 1, ref XGuid.dispenserIID, out ppb);
        OLE32.CoCreateInstance(ref XGuid.binderCLSID, null, 1, ref XGuid.binderIID, out pBnd);

        // Get the metadata dispenser from mscoree.dll
        Util.ComCheck(ppb != null, "Failed to create metadata dispenser");

        ((IMetaDataDispenserSubset)ppb).OpenScope(filename, 0, ref XGuid.importerIID, out pUnk);
        Util.ComCheck(pUnk != null, "Failed to open scope");

        importer = Marshal.GetComInterfaceForObject(pUnk, typeof(IMetadataImport));
        ((ISymUnmanagedBinder)pBnd).GetReaderForFile(importer, filename, null, out retVal);
        private_reader = (ISymUnmanagedReader)retVal;
      }
      catch (Exception x) {
        Console.WriteLine(x.Message);
      }
      finally {
        if (importer != IntPtr.Zero)
          Marshal.Release(importer);
      }
    }

    // ============================================================================
    // ========================== ISymbolReader Methods ===========================
    // ============================================================================

    /// <summary>
    /// This is the only SymbolReader method required by PERWAPI
    /// at this stage.
    /// </summary>
    /// <param name="tok">The metadata token</param>
    /// <returns>The SymbolMethod object for the token</returns>
    public ISymbolMethod GetMethod(SymbolToken tok) {
      // MIDL is ...
      //[PreserveSig]
      //int GetMethod(
      //    SymbolToken methodToken,
      //    [MarshalAs(UnmanagedType.Interface)] out ISymUnmanagedMethod retVal);
      ISymUnmanagedMethod unMeth = null;
      int hr = private_reader.GetMethod(tok, out unMeth);
      if (hr == OLE32.hr_E_FAIL)  // could be empty method
        return null;
      else                  // any other abnormal HRESULT
        Marshal.ThrowExceptionForHR(hr);
      // All ok ...
      return new SymbolMethod(unMeth);
    }

    // This one not used by PERWAPI yet.
    public ISymbolMethod GetMethod(SymbolToken tok, int ver) {
      ISymUnmanagedMethod unMeth = null;
      int hr = private_reader.GetMethodByVersion(tok, ver, out unMeth);
      if (hr == OLE32.hr_E_FAIL)  // could be empty method
        return null;
      else                  // any other abnormal HRESULT
        Marshal.ThrowExceptionForHR(hr);
      // All ok ...
      return new SymbolMethod(unMeth);
    }

    /// <summary>
    /// Gets user entry point for a reader.
    /// Returns null if reader is attached to a PE-file
    /// that does not have an entry point.
    /// </summary>
    public SymbolToken UserEntryPoint {
      get {
        SymbolToken retVal;
        int hr = private_reader.GetUserEntryPoint(out retVal);
        if (hr == OLE32.hr_E_FAIL)
          return new SymbolToken(0);
        else
          Marshal.ThrowExceptionForHR(hr);
        return retVal;
      }
    }

    #region unimplemented methods
    // Summary:
    //     Gets a document specified by the language, vendor, and type.
    //
    // Parameters:
    //   url:
    //     The URL that identifies the document.
    //
    //   documentType:
    //     The type of the document. You can specify this parameter as System.Guid.Empty.
    //
    //   languageVendor:
    //     The identity of the vendor for the document language. You can specify this
    //     parameter as System.Guid.Empty.
    //
    //   language:
    //     The document language. You can specify this parameter as System.Guid.Empty.
    //
    // Returns:
    //     The specified document.
    public ISymbolDocument GetDocument(
        string url,
        Guid language,
        Guid languageVendor,
        Guid documentType) {
      throw new NotImplementedException("No QUT.SymbolReader.GetDocument");
    }

    //
    // Summary:
    //     Gets an array of all documents defined in the symbol store.
    //
    // Returns:
    //     An array of all documents defined in the symbol store.
    public ISymbolDocument[] GetDocuments() {
      throw new NotImplementedException("No QUT.SymbolReader.GetDocuments");
    }

    //
    // Summary:
    //     Gets all global variables in the module.
    //
    // Returns:
    //     An array of all variables in the module.
    public ISymbolVariable[] GetGlobalVariables() {
      throw new NotImplementedException("No QUT.SymbolReader.GetGetGlobalVariables");
    }


    //
    // Summary:
    //     Gets the namespaces that are defined in the global scope within the current
    //     symbol store.
    //
    // Returns:
    //     The namespaces defined in the global scope within the current symbol store.
    public ISymbolNamespace[] GetNamespaces() {
      throw new NotImplementedException("No QUT.SymbolReader.GetNamespaces");
    }

    //
    // Summary:
    //     Gets an attribute value when given the attribute name.
    //
    // Parameters:
    //   name:
    //     The attribute name.
    //
    //   parent:
    //     The metadata token for the object for which the attribute is requested. 
    //
    // Returns:
    //     The value of the attribute.
    public byte[] GetSymAttribute(SymbolToken parent, string name) {
      throw new NotImplementedException("No QUT.SymbolReader.GetSymAttribute");
    }
    #endregion unimplemented methods

    /// <summary>
    /// Gets the method that contains a specified position of the document
    /// </summary>
    /// <param name="document">The document object</param>
    /// <param name="line">Source line number</param>
    /// <param name="column">Source column number</param>
    /// <returns>The chosen method</returns>
    public ISymbolMethod GetMethodFromDocumentPosition(ISymbolDocument doc, int line, int column) {
      ISymUnmanagedMethod unMeth = null;
      private_reader.GetMethodFromDocumentPosition(
          ((SymbolDocument)doc).WrappedDoc, line, column, out unMeth);
      return new SymbolMethod(unMeth);
    }

    public ISymbolVariable[] GetVariables(SymbolToken parent) {
      int varNm = 0;
      private_reader.GetVariables(parent, 0, out varNm, null);
      ISymUnmanagedVariable[] unVars = new ISymUnmanagedVariable[varNm];
      SymbolVariable[] retVal = new SymbolVariable[varNm];
      private_reader.GetVariables(parent, varNm, out varNm, unVars);
      for (int i = 0; i < varNm; i++)
        retVal[i] = new SymbolVariable(unVars[i]);
      return retVal;
    }
  }
  // ============================ End of SymbolReader ===========================

  /// <summary>
  /// This is the managed wrapper for the unmanaged
  /// Method descriptor.  This implements the interface
  /// System.Diagnostics.SymbolStore.ISymbolMethod
  /// </summary>
  public class SymbolMethod : ISymbolMethod {
    private const int INVALID = -1;
    private ISymUnmanagedMethod private_method;

    internal SymbolMethod(ISymUnmanagedMethod unMeth) {
      private_method = unMeth;
    }

    /// <summary>
    /// Gets the root scope of the method
    /// </summary>
    public ISymbolScope RootScope { // Needed by PERWAPI
      get {
        ISymUnmanagedScope retVal = null;
        private_method.GetRootScope(out retVal);
        return new SymbolScope(retVal);
      }
    }

    /// <summary>
    /// Gets the sequence point count for the method
    /// </summary>
    public int SequencePointCount { // Needed by PERWAPI
      get {
        int retVal = 0;
        private_method.GetSequencePointCount(out retVal);
        return retVal;
      }
    }

    /// <summary>
    /// Gets the symbol token for the method
    /// </summary>
    public SymbolToken Token {
      get {
        SymbolToken tok;
        private_method.GetToken(out tok);
        return tok;
      }
    }

    /// <summary>
    /// Gets the namespace for the method
    /// </summary>
    /// <returns>The namespace object</returns>
    public ISymbolNamespace GetNamespace() {
      ISymUnmanagedNamespace retVal = null;
      private_method.GetNamespace(out retVal);
      return new SymbolNamespace(retVal);
    }

    /// <summary>
    /// Gets the parameters of the method
    /// </summary>
    /// <returns>The method parameters</returns>
    public ISymbolVariable[] GetParameters() {
      int pNum = 0;

      // Call GetParameters just to get pNum
      private_method.GetParameters(0, out pNum, null);
      ISymUnmanagedVariable[] unVars = new ISymUnmanagedVariable[pNum];
      ISymbolVariable[] manVars = new ISymbolVariable[pNum];

      // Now call again to get the real information
      private_method.GetParameters(pNum, out pNum, unVars);
      for (int i = 0; i < pNum; i++)
        manVars[i] = new SymbolVariable(unVars[i]);
      return manVars;
    }

    #region unimplemented methods
    public int[] GetRanges(ISymbolDocument document, int line, int column) {
      throw new NotImplementedException("No QUT.SymbolMethod.GetRanges");
    }

    public ISymbolScope GetScope(int offset) {
      throw new NotImplementedException("No QUT.SymbolMethod.GetScope");
    }

    public int GetOffset(
        ISymbolDocument document,
        int line,
        int column) {
      throw new NotImplementedException("No QUT.SymbolMethod.GetOffset");
    }

    private void GetAndCheckLength(int[] arr, ref int num) {
      if (arr != null) {
        if (num == INVALID)
          num = arr.Length;
        else
          Util.ArgCheck(num == arr.Length, "Invalid arg to GetSequencePoints");
      }
    }

    public bool GetSourceStartEnd(ISymbolDocument[] docs, int[] lines, int[] columns) {
      throw new NotImplementedException("No QUT.SymbolMethod.GetSourceStartEnd");
    }
    #endregion unimplemented methods

    /// <summary>
    /// Gets the sequence points defined for this method
    /// </summary>
    /// <param name="offsets">array of IL offsets</param>
    /// <param name="documents">array of documents</param>
    /// <param name="lines">start line number array</param>
    /// <param name="columns">start column number array</param>
    /// <param name="endLines">end line number array</param>
    /// <param name="endColumns">start line number array</param>
    public void GetSequencePoints( // This method needed by PERWAPI
        int[] offsets,
        ISymbolDocument[] documents,
        int[] lines,
        int[] columns,
        int[] endLines,
        int[] endColumns) {
      int spCount = INVALID;
      GetAndCheckLength(offsets, ref spCount);
      GetAndCheckLength(lines, ref spCount);
      GetAndCheckLength(columns, ref spCount);
      GetAndCheckLength(endLines, ref spCount);
      GetAndCheckLength(endColumns, ref spCount);
      if (spCount == INVALID)
        spCount = 0;

      int dcCount = documents.Length;
      ISymUnmanagedDocument[] unDocs = new ISymUnmanagedDocument[dcCount];
      private_method.GetSequencePoints(
          dcCount, out spCount, offsets, unDocs, lines, columns, endLines, endColumns);
      for (int i = 0; i < dcCount; i++)
        documents[i] = new SymbolDocument(unDocs[i]);
      return;
    }
  }
  // ============================ End of SymbolMethod ===========================

  /// <summary>
  /// This class is a managed wrapper for the unmanaged
  /// Scope descriptor.  The defintions for ISymbolScope
  /// come from metadata.
  /// </summary>
  public class SymbolScope : ISymbolScope {
    private ISymUnmanagedScope private_scope;

    internal SymbolScope(ISymUnmanagedScope unScope) {
      private_scope = unScope;
    }

    /// <summary>
    /// Returns the end offset of the wrapped scope
    /// </summary>
    public int EndOffset {
      get {
        int offset;
        private_scope.GetEndOffset(out offset);
        return offset;
      }
    }

    /// <summary>
    /// Returns the method that contains the current lexical scope
    /// </summary>
    public ISymbolMethod Method {
      get {
        ISymUnmanagedMethod unMeth = null;
        private_scope.GetMethod(out unMeth);
        return new SymbolMethod(unMeth);
      }
    }

    /// <summary>
    /// Returns the parent lexical scope of the current scope
    /// </summary>
    public ISymbolScope Parent {
      get {
        ISymUnmanagedScope unScope = null;
        private_scope.GetParent(out unScope);
        return new SymbolScope(unScope);
      }
    }

    /// <summary>
    /// Returns the start offset of the current lexical scope
    /// </summary>
    public int StartOffset {
      get {
        int offset;
        private_scope.GetStartOffset(out offset);
        return offset;
      }
    }

    /// <summary>
    /// Returns the child lexical scopes of the current lexical scope.
    /// </summary>
    /// <returns></returns>
    public ISymbolScope[] GetChildren() {
      int chNum = 0;
      private_scope.GetChildren(0, out chNum, null);
      ISymUnmanagedScope[] unScps = new ISymUnmanagedScope[chNum];
      ISymbolScope[] manScps = new ISymbolScope[chNum];

      private_scope.GetChildren(chNum, out chNum, unScps);
      for (int i = 0; i < chNum; i++)
        manScps[i] = new SymbolScope(unScps[i]);
      return manScps;
    }

    /// <summary>
    /// Gets the local variables within the current lexical scope
    /// </summary>
    /// <returns>The local variables of the current scope</returns>
    public ISymbolVariable[] GetLocals() {
      int lcNum = 0;
      private_scope.GetLocals(0, out lcNum, null);
      ISymUnmanagedVariable[] unVars = new ISymUnmanagedVariable[lcNum];
      ISymbolVariable[] manVars = new ISymbolVariable[lcNum];

      private_scope.GetLocals(lcNum, out lcNum, unVars);
      for (int i = 0; i < lcNum; i++)
        manVars[i] = new SymbolVariable(unVars[i]);
      return manVars;
    }

    /// <summary>
    /// Gets the namespaces that are used within the current scope
    /// </summary>
    /// <returns>The namespaces that are used within the current scope</returns>
    public ISymbolNamespace[] GetNamespaces() {
      int nmNum = 0;
      private_scope.GetNamespaces(0, out nmNum, null);
      ISymUnmanagedNamespace[] unNams = new ISymUnmanagedNamespace[nmNum];
      ISymbolNamespace[] manNams = new ISymbolNamespace[nmNum];

      private_scope.GetNamespaces(nmNum, out nmNum, unNams);
      for (int i = 0; i < nmNum; i++)
        manNams[i] = new SymbolNamespace(unNams[i]);
      return manNams;

    }
  }
  // ============================ End of SymbolScope ===========================

  /// <summary>
  /// Managed wrapper for ISymUnmanagedNamespace
  /// </summary>
  public class SymbolNamespace : ISymbolNamespace {
    private ISymUnmanagedNamespace private_namespace;

    internal SymbolNamespace(ISymUnmanagedNamespace unNmsp) {
      private_namespace = unNmsp;
    }

    // Summary:
    //     Gets the current namespace.
    //
    // Returns:
    //     The current namespace.
    public string Name {
      get {
        StringBuilder bldr;
        int nmLen = 0;
        private_namespace.GetName(0, out nmLen, null);
        bldr = new StringBuilder(nmLen);
        private_namespace.GetName(nmLen, out nmLen, bldr);
        return bldr.ToString();
      }
    }

    // Summary:
    //     Gets the child members of the current namespace.
    //
    // Returns:
    //     The child members of the current namespace.
    public ISymbolNamespace[] GetNamespaces() {
      throw new NotImplementedException("No QUT.SymbolNamespace.GetNamespaces");
    }
    //
    // Summary:
    //     Gets all the variables defined at global scope within the current namespace.
    //
    // Returns:
    //     The variables defined at global scope within the current namespace.
    public ISymbolVariable[] GetVariables() {
      throw new NotImplementedException("No QUT.SymbolNamespace.GetVariables");
    }
  }
  // ============================ End of SymbolReader ===========================

  /// <summary>
  /// Managed wrapper for ISymUnmanagedDocument
  /// </summary>
  public class SymbolDocument : ISymbolDocument {
    private ISymUnmanagedDocument private_document;

    /// <summary>
    /// Constructor for SymbolDocument
    /// </summary>
    /// <param name="unDoc"></param>
    internal SymbolDocument(ISymUnmanagedDocument unDoc) {
      private_document = unDoc;
    }

    /// <summary>
    /// Gets the wrapped document
    /// </summary>
    internal ISymUnmanagedDocument WrappedDoc {
      get { return private_document; }
    }

    /// <summary>
    /// Returns a GUID identifying the checksum algorithm.
    /// Returns Guid.Zero if there is no checksum.
    /// </summary>
    public Guid CheckSumAlgorithmId {
      get {
        Guid retVal = new Guid();
        private_document.GetCheckSumAlgorithmId(ref retVal);
        return retVal;
      }
    }

    /// <summary>
    /// Gets the document type guid
    /// </summary>
    public Guid DocumentType {
      get {
        Guid retVal = new Guid();
        private_document.GetDocumentType(ref retVal);
        return retVal;
      }
    }

    /// <summary>
    /// Value is true if document is stored in the symbol store, otherwise false.
    /// </summary>
    public bool HasEmbeddedSource {
      get {
        bool retVal = false;
        private_document.HasEmbeddedSource(out retVal);
        return retVal;
      }
    }

    /// <summary>
    /// Gets the language guid
    /// </summary>
    public Guid Language {
      get {
        Guid retVal = new Guid();
        private_document.GetLanguage(ref retVal);
        return retVal;
      }
    }

    /// <summary>
    /// Gets the language vendor guid
    /// </summary>
    public Guid LanguageVendor {
      get {
        Guid retVal = new Guid();
        private_document.GetLanguageVendor(ref retVal);
        return retVal;
      }
    }

    /// <summary>
    /// Gets the source length of the current document
    /// </summary>
    public int SourceLength {
      get {
        int retVal = 0;
        private_document.GetSourceLength(out retVal);
        return retVal;
      }
    }

    /// <summary>
    /// Gets the URL of the current document
    /// </summary>
    public string URL {
      get {
        int strLen;
        private_document.GetURL(0, out strLen, null);
        StringBuilder retVal = new StringBuilder(strLen);
        private_document.GetURL(strLen, out strLen, retVal);
        return retVal.ToString();
      }
    }


    /// <summary>
    /// Gets the closest line that has a sequence point
    /// </summary>
    /// <param name="line">A line in the document</param>
    /// <returns>The closest line with a sequence point</returns>
    public int FindClosestLine(int line) {
      int retVal;
      private_document.FindClosestLine(line, out retVal);
      return retVal;
    }

    #region unimplemented methods
    /// <summary>
    /// Gets the checksum
    /// </summary>
    /// <returns>The checksum</returns>
    public byte[] GetCheckSum() {
      throw new NotImplementedException("No QUT.SymbolDocument.GetCheckSum");
    }

    //
    // Summary:
    //     Gets the embedded document source for the specified range.
    //
    // Parameters:
    //   startLine:
    //     The starting line in the current document.
    //
    //   endLine:
    //     The ending line in the current document.
    //
    //   startColumn:
    //     The starting column in the current document.
    //
    //   endColumn:
    //     The ending column in the current document.
    //
    // Returns:
    //     The document source for the specified range.
    public byte[] GetSourceRange(int startLine, int startColumn, int endLine, int endColumn) {
      throw new NotImplementedException("No QUT.SymbolDocument.GetSourceRange");
    }
    #endregion unimplemented methods
  }
  // ========================== End of SymbolDocument ===========================

  /// <summary>
  /// Managed wrapper for ISymUnmanagedVariable
  /// </summary>
  public class SymbolVariable : ISymbolVariable {
    private ISymUnmanagedVariable private_variable;

    internal SymbolVariable(ISymUnmanagedVariable unVar) {
      private_variable = unVar;
    }

    /// <summary>
    /// Gets the first address of the variable
    /// </summary>
    public int AddressField1 {
      get {
        int retVal;
        private_variable.GetAddressField1(out retVal);
        return retVal;
      }
    }

    /// <summary>
    /// Gets the second address of the variable
    /// </summary>
    public int AddressField2 {
      get {
        int retVal;
        private_variable.GetAddressField2(out retVal);
        return retVal;
      }
    }

    /// <summary>
    /// Gets the third address of the variable
    /// </summary>
    public int AddressField3 {
      get {
        int retVal;
        private_variable.GetAddressField3(out retVal);
        return retVal;
      }
    }

    /// <summary>
    /// Gets the type of the address. The result is a
    /// System.Diagnostics.SymbolStore.SymAddressKind
    /// enumeration value.
    /// </summary>
    public SymAddressKind AddressKind {
      get {
        int retVal;
        private_variable.GetAddressKind(out retVal);
        return (SymAddressKind)retVal;
      }
    }

    /// <summary>
    /// Gets the variable attributes
    /// </summary>
    public object Attributes {
      get {
        int retVal;
        private_variable.GetAttributes(out retVal);
        return retVal;
      }
    }

    /// <summary>
    /// Gets the end offset of the variable
    /// </summary>
    public int EndOffset {
      get {
        int retVal;
        private_variable.GetEndOffset(out retVal);
        return retVal;
      }
    }

    /// <summary>
    /// Gets the name of the variable
    /// </summary>
    public string Name {
      get {
        StringBuilder bldr;
        int nmLen = 0;
        private_variable.GetName(0, out nmLen, null);
        bldr = new StringBuilder(nmLen);
        private_variable.GetName(nmLen, out nmLen, bldr);
        return bldr.ToString();
      }
    }

    /// <summary>
    /// Gets the start offset of the variable
    /// </summary>
    public int StartOffset {
      get {
        int retVal;
        private_variable.GetStartOffset(out retVal);
        return retVal;
      }
    }

    /// <summary>
    /// Gets the variable signature
    /// </summary>
    /// <returns>The signature blob</returns>
    public byte[] GetSignature() {
      int sgLen;
      private_variable.GetSignature(0, out sgLen, null);
      byte[] retVal = new byte[sgLen];
      private_variable.GetSignature(sgLen, out sgLen, retVal);
      return retVal;
    }
  }
  // ========================== End of SymbolVariable ===========================
}
