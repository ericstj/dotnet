// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Security;
using ComTypes = System.Runtime.InteropServices.ComTypes;

namespace Microsoft.CSharp.RuntimeBinder.ComInterop
{
    internal static class ComRuntimeHelpers
    {
        [RequiresUnreferencedCode(Binder.TrimmerWarning)]
        [RequiresDynamicCode(Binder.DynamicCodeWarning)]
        public static void CheckThrowException(int hresult, ref ExcepInfo excepInfo, uint argErr, string message)
        {
            if (ComHresults.IsSuccess(hresult))
            {
                return;
            }

            switch (hresult)
            {
                case ComHresults.DISP_E_BADPARAMCOUNT:
                    // The number of elements provided to DISPPARAMS is different from the number of arguments
                    // accepted by the method or property.
                    throw Error.DispBadParamCount(message);

                case ComHresults.DISP_E_BADVARTYPE:
                    //One of the arguments in rgvarg is not a valid variant type.
                    break;

                case ComHresults.DISP_E_EXCEPTION:
                    // The application needs to raise an exception. In this case, the structure passed in pExcepInfo
                    // should be filled in.
                    throw excepInfo.GetException();

                case ComHresults.DISP_E_MEMBERNOTFOUND:
                    // The requested member does not exist, or the call to Invoke tried to set the value of a
                    // read-only property.
                    throw Error.DispMemberNotFound(message);

                case ComHresults.DISP_E_NONAMEDARGS:
                    // This implementation of IDispatch does not support named arguments.
                    throw Error.DispNoNamedArgs(message);

                case ComHresults.DISP_E_OVERFLOW:
                    // One of the arguments in rgvarg could not be coerced to the specified type.
                    throw Error.DispOverflow(message);

                case ComHresults.DISP_E_PARAMNOTFOUND:
                    // One of the parameter DISPIDs does not correspond to a parameter on the method. In this case,
                    // puArgErr should be set to the first argument that contains the error.
                    break;

                case ComHresults.DISP_E_TYPEMISMATCH:
                    // One or more of the arguments could not be coerced. The index within rgvarg of the first
                    // parameter with the incorrect type is returned in the puArgErr parameter.
                    throw Error.DispTypeMismatch(argErr, message);

                case ComHresults.DISP_E_UNKNOWNINTERFACE:
                    // The interface identifier passed in riid is not IID_NULL.
                    break;

                case ComHresults.DISP_E_UNKNOWNLCID:
                    // The member being invoked interprets string arguments according to the LCID, and the
                    // LCID is not recognized.
                    break;

                case ComHresults.DISP_E_PARAMNOTOPTIONAL:
                    // A required parameter was omitted.
                    throw Error.DispParamNotOptional(message);
            }

            Marshal.ThrowExceptionForHR(hresult);
        }

        internal static void GetInfoFromType(ComTypes.ITypeInfo typeInfo, out string name, out string documentation)
        {
            typeInfo.GetDocumentation(-1, out name, out documentation, out int _, out string _);
        }

        internal static string GetNameOfMethod(ComTypes.ITypeInfo typeInfo, int memid)
        {
            string[] rgNames = new string[1];
            typeInfo.GetNames(memid, rgNames, 1, out int _);
            return rgNames[0];
        }

        internal static string GetNameOfLib(ComTypes.ITypeLib typeLib)
        {
            typeLib.GetDocumentation(-1, out string name, out string _, out int _, out string _);
            return name;
        }

        internal static string GetNameOfType(ComTypes.ITypeInfo typeInfo)
        {
            GetInfoFromType(typeInfo, out string name, out string _);

            return name;
        }

        /// <summary>
        /// Look for type info using IDispatch.GetTypeInfo
        /// </summary>
        /// <param name="dispatch">IDispatch object</param>
        /// <remarks>
        /// Some COM objects just don't expose type info. In these cases, this method will return null.
        /// Some COM objects do intend to expose type info, but may not be able to do so if the type library
        /// is not properly registered. This will be considered an error.
        /// </remarks>
        /// <returns>Type info</returns>
        internal static ComTypes.ITypeInfo GetITypeInfoFromIDispatch(IDispatch dispatch)
        {
            int hresult = dispatch.TryGetTypeInfoCount(out uint typeCount);
            if (typeCount == 0)
            {
                // COM objects should return a type count of 0 to indicate that type info is not exposed.
                // Some COM objects may return a non-success HRESULT when type info is not supported, so
                // we only check the count and not the HRESULT in this case.
                return null;
            }

            Marshal.ThrowExceptionForHR(hresult);
            Debug.Assert(typeCount == 1);

            IntPtr typeInfoPtr;
            hresult = dispatch.TryGetTypeInfo(0, 0, out typeInfoPtr);
            if (!ComHresults.IsSuccess(hresult))
            {
                // Word.Basic always returns this because of an incorrect implementation of IDispatch.GetTypeInfo
                // Any implementation that returns E_NOINTERFACE is likely to do so in all environments
                if (hresult == ComHresults.E_NOINTERFACE)
                {
                    return null;
                }

                // This assert is potentially over-restrictive since COM components can behave in quite unexpected ways.
                // However, asserting the common expected cases ensures that we find out about the unexpected scenarios, and
                // can investigate the scenarios to ensure that there is no bug in our own code.
                Debug.Assert(hresult == ComHresults.TYPE_E_LIBNOTREGISTERED);

                Marshal.ThrowExceptionForHR(hresult);
            }

            if (typeInfoPtr == IntPtr.Zero)
            {
                Marshal.ThrowExceptionForHR(ComHresults.E_FAIL);
            }

            ComTypes.ITypeInfo typeInfo = null;
            try
            {
                typeInfo = Marshal.GetObjectForIUnknown(typeInfoPtr) as ComTypes.ITypeInfo;
            }
            finally
            {
                Marshal.Release(typeInfoPtr);
            }

            return typeInfo;
        }

        internal static unsafe ComTypes.TYPEATTR GetTypeAttrForTypeInfo(ComTypes.ITypeInfo typeInfo)
        {
            IntPtr pAttrs;
            typeInfo.GetTypeAttr(out pAttrs);

            // GetTypeAttr should never return null, this is just to be safe
            if (pAttrs == IntPtr.Zero)
            {
                throw Error.CannotRetrieveTypeInformation();
            }

            try
            {
                return *(ComTypes.TYPEATTR*)pAttrs;
            }
            finally
            {
                typeInfo.ReleaseTypeAttr(pAttrs);
            }
        }

        internal static unsafe ComTypes.TYPELIBATTR GetTypeAttrForTypeLib(ComTypes.ITypeLib typeLib)
        {
            IntPtr pAttrs;
            typeLib.GetLibAttr(out pAttrs);

            // GetTypeAttr should never return null, this is just to be safe
            if (pAttrs == IntPtr.Zero)
            {
                throw Error.CannotRetrieveTypeInformation();
            }

            try
            {
                return *(ComTypes.TYPELIBATTR*)pAttrs;
            }
            finally
            {
                typeLib.ReleaseTLibAttr(pAttrs);
            }
        }

        [RequiresUnreferencedCode(Binder.TrimmerWarning)]
        [RequiresDynamicCode(Binder.DynamicCodeWarning)]
        public static BoundDispEvent CreateComEvent(object rcw, Guid sourceIid, int dispid)
        {
            return new BoundDispEvent(rcw, sourceIid, dispid);
        }

        [RequiresDynamicCode(Binder.DynamicCodeWarning)]
        public static DispCallable CreateDispCallable(IDispatchComObject dispatch, ComMethodDesc method)
        {
            return new DispCallable(dispatch, method.Name, method.DispId);
        }
    }

    /// <summary>
    /// This class contains methods that either cannot be expressed in C#, or which require writing unsafe code.
    /// Callers of these methods need to use them extremely carefully as incorrect use could cause GC-holes
    /// and other problems.
    /// </summary>
    ///
    internal static class UnsafeMethods
    {
        #region public members

        public static unsafe IntPtr ConvertInt32ByrefToPtr(ref int value) { return (IntPtr)System.Runtime.CompilerServices.Unsafe.AsPointer(ref value); }
        public static unsafe IntPtr ConvertVariantByrefToPtr(ref ComVariant value) { return (IntPtr)System.Runtime.CompilerServices.Unsafe.AsPointer(ref value); }

        internal static ComVariant GetVariantForObject(object obj)
        {
            ComVariant variant = default;
            if (obj == null)
            {
                return variant;
            }
            InitVariantForObject(obj, ref variant);
            return variant;
        }

        internal static void InitVariantForObject(object obj, ref ComVariant variant)
        {
            Debug.Assert(obj != null);

            // GetNativeVariantForObject is very expensive for values that marshal as VT_DISPATCH
            // also it is extremely common scenario when object at hand is an RCW.
            // Therefore we are going to test for IDispatch before defaulting to GetNativeVariantForObject.
            if (obj is IDispatch)
            {
                variant = ComVariant.CreateRaw(VarEnum.VT_DISPATCH, obj is not null ? Marshal.GetIDispatchForObject(obj) : 0);
                return;
            }

            Marshal.GetNativeVariantForObject(obj, ConvertVariantByrefToPtr(ref variant));
        }

        // This method is intended for use through reflection and should not be used directly
        public static object GetObjectForVariant(ComVariant variant)
        {
            IntPtr ptr = UnsafeMethods.ConvertVariantByrefToPtr(ref variant);
            return Marshal.GetObjectForNativeVariant(ptr);
        }

        // This method is intended for use through reflection and should only be used directly by IUnknownReleaseNotZero
        public static unsafe int IUnknownRelease(IntPtr interfacePointer)
        {
            return ((delegate* unmanaged<IntPtr, int>)(*(*(void***)interfacePointer + 2 /* IUnknown.Release slot */)))(interfacePointer);
        }

        // This method is intended for use through reflection and should not be used directly
        public static void IUnknownReleaseNotZero(IntPtr interfacePointer)
        {
            if (interfacePointer != IntPtr.Zero)
            {
                IUnknownRelease(interfacePointer);
            }
        }

        // This method is intended for use through reflection and should not be used directly
        public static unsafe int IDispatchInvoke(
            IntPtr dispatchPointer,
            int memberDispId,
            ComTypes.INVOKEKIND flags,
            ref ComTypes.DISPPARAMS dispParams,
            out ComVariant result,
            out ExcepInfo excepInfo,
            out uint argErr)
        {
            Guid IID_NULL = default;

            fixed (ComTypes.DISPPARAMS* pDispParams = &dispParams)
            fixed (ComVariant* pResult = &result)
            fixed (ExcepInfo* pExcepInfo = &excepInfo)
            fixed (uint* pArgErr = &argErr)
            {
                var pfnIDispatchInvoke = (delegate* unmanaged<IntPtr, int, Guid*, int, ushort, ComTypes.DISPPARAMS*, ComVariant*, ExcepInfo*, uint*, int>)
                    (*(*(void***)dispatchPointer + 6 /* IDispatch.Invoke slot */));

                int hresult = pfnIDispatchInvoke(dispatchPointer,
                    memberDispId, &IID_NULL, 0, (ushort)flags, pDispParams, pResult, pExcepInfo, pArgErr);

                if (hresult == ComHresults.DISP_E_MEMBERNOTFOUND
                    && (flags & ComTypes.INVOKEKIND.INVOKE_FUNC) != 0
                    && (flags & (ComTypes.INVOKEKIND.INVOKE_PROPERTYPUT | ComTypes.INVOKEKIND.INVOKE_PROPERTYPUTREF)) == 0)
                {
                    // Re-invoke with no result argument to accommodate Word
                    hresult = pfnIDispatchInvoke(dispatchPointer,
                        memberDispId, &IID_NULL, 0, (ushort)ComTypes.INVOKEKIND.INVOKE_FUNC, pDispParams, null, pExcepInfo, pArgErr);
                }

                return hresult;
            }
        }

        // This method is intended for use through reflection and should not be used directly
        public static IntPtr GetIdsOfNamedParameters(IDispatch dispatch, string[] names, int methodDispId, out GCHandle pinningHandle)
        {
            pinningHandle = GCHandle.Alloc(null, GCHandleType.Pinned);
            int[] dispIds = new int[names.Length];
            Guid empty = Guid.Empty;
            int hresult = dispatch.TryGetIDsOfNames(ref empty, names, (uint)names.Length, 0, dispIds);
            if (hresult < 0)
            {
                Marshal.ThrowExceptionForHR(hresult);
            }

            if (methodDispId != dispIds[0])
            {
                throw Error.GetIDsOfNamesInvalid(names[0]);
            }

            int[] keywordArgDispIds = dispIds.RemoveFirst(); // Remove the dispId of the method name

            pinningHandle.Target = keywordArgDispIds;
            return Marshal.UnsafeAddrOfPinnedArrayElement(keywordArgDispIds, 0);
        }

        #endregion

        #region non-public members

        private static readonly object s_lock = new object();
        private static ModuleBuilder s_dynamicModule;

        internal static ModuleBuilder DynamicModule
        {
            [RequiresDynamicCode(Binder.DynamicCodeWarning)]
            get
            {
                if (s_dynamicModule != null)
                {
                    return s_dynamicModule;
                }
                lock (s_lock)
                {
                    if (s_dynamicModule == null)
                    {
                        string name = typeof(VariantArray).Namespace + ".DynamicAssembly";
                        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run);
                        s_dynamicModule = assembly.DefineDynamicModule(name);
                    }
                    return s_dynamicModule;
                }
            }
        }

        #endregion
    }
}
