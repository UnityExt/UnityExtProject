using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityExt.Tools {

    /// <summary>
    /// Auxiliary class to make DocFX calls and generate the documentation for the UnityExt project.
    /// </summary>
    public class DocumentationTools {
    
        /// <summary>
        /// Documentation root.
        /// </summary>
        static public string docfxRoot { 
            get { 
                DirectoryInfo di = Directory.CreateDirectory(Application.dataPath);
                string p = di.Parent.FullName+"/Documentation/";
                return p.Replace("\\","/");
            }
        }

        /// <summary>
        /// Path to the docfx executable
        /// </summary>
        static public string docfxExecutablePath = $"{docfxRoot}docfx/docfx.exe";

        /// <summary>
        /// Path to the docfx project json config.
        /// </summary>
        static public string docfxProjectJson    = $"{docfxRoot}project/docfx.json";

        /// <summary>
        /// Path to the docfx resulting build folder
        /// </summary>
        static public string docfxBuildFolder    = $"{docfxRoot}_site/";

        /// <summary>
        /// Path to the docfx target build folder
        /// </summary>
        static public string docfxBuildTargetFolder    = $"{docfxRoot}build/";

        /// <summary>
        /// Builds the documentation (windows only).
        /// </summary>
        #if UNITY_EDITOR_WIN
        [MenuItem("File/Build UnityExt Documentation",false,207)]
        #endif
        static public void Build() {

            System.Diagnostics.Process proc;            
            System.Diagnostics.ProcessStartInfo proc_info;
            string  args;

            args = string.Join(" ",
            new string[] {                
                "metadata",
                $"\"{docfxProjectJson}\"",
                "-f",
                "--raw",
                $"-l {docfxRoot}docfx-metadata-result.log --loglevel Verbose"
            });

            Debug.Log($"DocumentationTools> Build / Calling Metadata\n{docfxExecutablePath} {args}");
            
            proc_info = new System.Diagnostics.ProcessStartInfo(docfxExecutablePath,args);            
            proc      = System.Diagnostics.Process.Start(proc_info);
            proc.WaitForExit();

            Debug.Log($"DocumentationTools> Build / Metadata Complete!");

            args = string.Join(" ",
            new string[] {                
                "build",
                $"\"{docfxProjectJson}\"",
                $"-o \"{docfxRoot}\"",
                "-f",                
                $"-l {docfxRoot}docfx-build-result.log --loglevel Verbose"
            });

            Debug.Log($"DocumentationTools> Build / Starting Build\n{docfxExecutablePath} {args}");

            proc_info = new System.Diagnostics.ProcessStartInfo(docfxExecutablePath,args);            
            proc      = System.Diagnostics.Process.Start(proc_info);
            proc.WaitForExit();

            Debug.Log($"DocumentationTools> Build / Complete!");

            string[] build_files;
            
            //Delete old files if exists.           
            if(Directory.Exists(docfxBuildTargetFolder)) {
                build_files = Directory.GetFiles(docfxBuildTargetFolder,"*", SearchOption.AllDirectories);
                for(int i=0;i<build_files.Length;i++) File.Delete(build_files[i]);
            }
            else {
                //Create if fresh build
                Directory.CreateDirectory(docfxBuildTargetFolder);
            }            
            //Fetch new build files and move them
            build_files = Directory.GetFiles(docfxBuildFolder,"*", SearchOption.AllDirectories);
            for(int i = 0; i<build_files.Length; i++) {
                string fn = build_files[i];
                //Replace _site folder by 'build'
                fn = fn.Replace("_site","build");
                File.Move(build_files[i],fn);
            }
            //Delete empty '_site' folder
            Directory.Delete(docfxBuildFolder,true);

        }



    }
}