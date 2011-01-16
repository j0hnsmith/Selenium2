﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text;
using OpenQA.Selenium;
using OpenQA.Selenium.IE;
using OpenQA.Selenium.Remote;

namespace OpenQA.Selenium.IE
{
    /// <summary>
    /// Provides a wrapper for the native-code Internet Explorer driver library.
    /// </summary>
    internal class NativeDriverLibrary
    {
        #region Private constants
        private const string LibraryName = "IEDriver.dll";
        private const string StartServerFunctionName = "StartServer";
        private const string StopServerFunctionName = "StopServer";
        #endregion

        #region Private member variables
        private static NativeDriverLibrary libraryInstance;
        private static object lockObject = new object();
        private static Random tempFileGenerator = new Random();

        private IntPtr nativeLibraryHandle = IntPtr.Zero;
        private IntPtr serverHandle = IntPtr.Zero;
        private int refCount;
        #endregion

        #region Constructor/Destructor
        /// <summary>
        /// Prevents a default instance of the <see cref="NativeDriverLibrary"/> class from being created.
        /// </summary>
        /// <remarks>This is a singleton class, so it does not require instantiation by consumers. They
        /// should use the Instance property instead.</remarks>
        private NativeDriverLibrary()
        {
            string nativeLibraryPath = GetNativeLibraryPath();
            nativeLibraryHandle = NativeMethods.LoadLibrary(nativeLibraryPath);
            int errorCode = Marshal.GetLastWin32Error();
            if (nativeLibraryHandle == IntPtr.Zero)
            {
                throw new WebDriverException(string.Format(CultureInfo.InvariantCulture, "An error (code: {0}) occured while attempting to load the native code library", errorCode));
            }

        }
        #endregion

        #region Private delegates
        private delegate IntPtr StartServerFunction(int port);
        private delegate void StopServerFunction(IntPtr serverHandle);
        //private delegate void StartServerFunction(int port);
        //private delegate void StopServerFunction();
        #endregion

        #region Singleton instance property
        /// <summary>
        /// Gets the singleton instance of the <see cref="NativeDriverLibrary"/> class.
        /// </summary>
        internal static NativeDriverLibrary Instance
        {
            get
            {
                lock (lockObject)
                {
                    if (libraryInstance == null)
                    {
                        libraryInstance = new NativeDriverLibrary();
                    }

                    return libraryInstance;
                }
            }
        }
        #endregion

        public void StartServer(int port)
        {
            //if (serverHandle == IntPtr.Zero || refCount == 0)
            if (refCount == 0)
            {
                IntPtr functionPointer = NativeMethods.GetProcAddress(nativeLibraryHandle, StartServerFunctionName);
                StartServerFunction startServerFunction = Marshal.GetDelegateForFunctionPointer(functionPointer, typeof(StartServerFunction)) as StartServerFunction;
                //startServerFunction(port);
                serverHandle = startServerFunction(port);
                if (serverHandle == IntPtr.Zero)
                {
                    throw new WebDriverException("An error occured while attempting to start the HTTP server");
                }
            }

            refCount++;
        }

        public void StopServer()
        {
            refCount--;
            if (refCount == 0)
            {
                IntPtr functionPointer = NativeMethods.GetProcAddress(nativeLibraryHandle, StopServerFunctionName);
                StopServerFunction stopServerFunction = Marshal.GetDelegateForFunctionPointer(functionPointer, typeof(StopServerFunction)) as StopServerFunction;
                stopServerFunction(serverHandle);
                //stopServerFunction();
                serverHandle = IntPtr.Zero;
            }
        }

        #region Private methods
        private static string GetNativeLibraryResourceName()
        {
            // We're compiled as Any CPU, which will run as a 64-bit process
            // on 64-bit OS, and 32-bit process on 32-bit OS. Thus, checking
            // the size of IntPtr is good enough.
            string resourceName = "WebDriver.InternetExplorerDriver.{0}.dll";
            if (IntPtr.Size == 8)
            {
                resourceName = string.Format(CultureInfo.InvariantCulture, resourceName, "x64");
            }
            else
            {
                resourceName = string.Format(CultureInfo.InvariantCulture, resourceName, "x86");
            }

            return resourceName;
        }

        private static void ExtractNativeLibrary(string nativeLibraryPath, Stream libraryStream)
        {
            FileStream outputStream = File.Create(nativeLibraryPath);
            byte[] buffer = new byte[1000];
            int bytesRead = libraryStream.Read(buffer, 0, buffer.Length);
            while (bytesRead > 0)
            {
                outputStream.Write(buffer, 0, bytesRead);
                bytesRead = libraryStream.Read(buffer, 0, buffer.Length);
            }

            outputStream.Close();
            libraryStream.Close();
        }

        private static string GetNativeLibraryPath()
        {
            Assembly executingAssembly = Assembly.GetExecutingAssembly();
            string currentDirectory = executingAssembly.Location;

            // If we're shadow copying,. fiddle with 
            // the codebase instead 
            if (AppDomain.CurrentDomain.ShadowCopyFiles)
            {
                Uri uri = new Uri(executingAssembly.CodeBase);
                currentDirectory = uri.LocalPath;
            }

            string nativeLibraryPath = Path.Combine(Path.GetDirectoryName(currentDirectory), LibraryName);
            if (!File.Exists(nativeLibraryPath))
            {
                string resourceName = GetNativeLibraryResourceName();

                if (executingAssembly.GetManifestResourceInfo(resourceName) == null)
                {
                    throw new WebDriverException("The native code library (InternetExplorerDriver.dll) could not be found in the current directory nor as an embedded resource.");
                }

                string nativeLibraryFolderName = string.Format(CultureInfo.InvariantCulture, "webdriver{0}libs", tempFileGenerator.Next());
                string nativeLibraryDirectory = Path.Combine(Path.GetTempPath(), nativeLibraryFolderName);
                if (!Directory.Exists(nativeLibraryDirectory))
                {
                    Directory.CreateDirectory(nativeLibraryDirectory);
                }

                nativeLibraryPath = Path.Combine(nativeLibraryDirectory, LibraryName);
                Stream libraryStream = executingAssembly.GetManifestResourceStream(resourceName);
                ExtractNativeLibrary(nativeLibraryPath, libraryStream);
            }

            return nativeLibraryPath;
        }

        private static void DeleteLibraryDirectory(string nativeLibraryDirectory)
        {
            int numberOfRetries = 0;
            while (Directory.Exists(nativeLibraryDirectory) && numberOfRetries < 10)
            {
                try
                {
                    Directory.Delete(nativeLibraryDirectory, true);
                }
                catch (IOException)
                {
                    // If we hit an exception (like file still in use), wait a half second
                    // and try again. If we still hit an exception, go ahead and let it through.
                    System.Threading.Thread.Sleep(500);
                }
                catch (UnauthorizedAccessException)
                {
                    // If we hit an exception (like file still in use), wait a half second
                    // and try again. If we still hit an exception, go ahead and let it through.
                    System.Threading.Thread.Sleep(500);
                }
                finally
                {
                    numberOfRetries++;
                }

                if (Directory.Exists(nativeLibraryDirectory))
                {
                    Console.WriteLine("Unable to delete native library directory '{0}'", nativeLibraryDirectory);
                }
            }
        }
        #endregion
    }
}