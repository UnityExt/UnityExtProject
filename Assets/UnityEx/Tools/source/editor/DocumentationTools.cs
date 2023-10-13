using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityExt.Project {

    /// <summary>
    /// Auxiliary class to make DocFX calls and generate the documentation for the UnityExt project.
    /// References:
    /// https://dotnet.github.io/docfx/tutorial/docfx_getting_started.html
    /// https://dotnet.github.io/docfx/tutorial/intro_template.html
    /// https://dotnet.github.io/docfx/templates-and-plugins/templates-dashboard.html
    /// </summary>
    public class DocumentationTools {
    
        //docfx.exe pdf project/docfx.json --name unityext-documentation -o ./unityext.github.io/pdf/ -t templates/pdf.default

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
        static public string docfxBuildTempFolder    = $"build-temp";

        /// <summary>
        /// Target build folder bane.
        /// </summary>
        static public string docfxBuildTargetFolder  = $"unityext.github.io";

        /// <summary>
        /// Path to the docfx resulting build folder
        /// </summary>
        static public string docfxBuildTempPath      = $"{docfxRoot}{docfxBuildTempFolder}/";

        /// <summary>
        /// Path to the docfx target build folder
        /// </summary>
        static public string docfxBuildTargetPath    = $"{docfxRoot}{docfxBuildTargetFolder}/";

        /// <summary>
        /// Internals.
        /// </summary>
        static float  m_build_progress;
        static string m_build_step;
        static bool   m_build_cancel;

        /// <summary>
        /// Builds the documentation (windows only).
        /// </summary>
        #if UNITY_EDITOR_WIN
        [MenuItem("UnityExt/Documentation/Build")]
        #endif
        static async public void Build() {

            System.Diagnostics.Process proc;            
            System.Diagnostics.ProcessStartInfo proc_info;
            string  args;
            System.Threading.Tasks.Task tsk;

            m_build_progress = 0f;
            m_build_step     = "";
            m_build_cancel   = false;
            EditorApplication.CallbackFunction on_build_loop = null;

            on_build_loop = 
            delegate() {         
                bool is_compiling = EditorApplication.isCompiling;
                if(is_compiling || m_build_cancel || (m_build_progress>=1f))       { EditorApplication.update -= on_build_loop; EditorUtility.ClearProgressBar();  return; }                
                m_build_cancel = EditorUtility.DisplayCancelableProgressBar("Building UnityExt Docs",m_build_step,m_build_progress);
            };

            //Start feedback loop
            EditorApplication.update += on_build_loop;

            args = string.Join(" ",
            new string[] { 
                //Command
                "metadata",
                //Target project
                $"\"{docfxProjectJson}\"",
                //Force rebuild
                //"-f",
                //Use /// comment data
                //"--raw",
                //Logging
                $"-l {docfxRoot}docfx-metadata-result.log --log Verbose"
            });

            m_build_progress = 0f;
            m_build_step     = "Generating Metadata";

            tsk = new System.Threading.Tasks.Task(delegate() {
                Debug.Log($"Documentation> Running DocFX Metadata\n{docfxExecutablePath} {string.Join(" ",args)}");
                proc_info = new System.Diagnostics.ProcessStartInfo(docfxExecutablePath,args);
                proc_info.UseShellExecute = false;
                proc_info.CreateNoWindow  = true;
                proc      = System.Diagnostics.Process.Start(proc_info);
                proc.WaitForExit();
            });
            tsk.Start();
            await tsk;

            if(m_build_cancel) return;

            args = string.Join(" ",
            new string[] {    
                //Command
                "build",
                //Project source
                $"\"{docfxProjectJson}\"",
                //Output folder
                $"-o \"{docfxRoot}\"",
                //Force to rebuild
                //"-f",     
                //Template
                "-t templates/default,templates/unity",
                //Logging
                $"-l {docfxRoot}docfx-build-result.log --log Verbose"
            });

            
            m_build_progress = 0.25f;
            m_build_step     = "Building Docs";

            tsk = new System.Threading.Tasks.Task(delegate() {
                Debug.Log($"Documentation> Running DocFX Build\n{docfxExecutablePath} {string.Join(" ",args)}");
                proc_info = new System.Diagnostics.ProcessStartInfo(docfxExecutablePath,args);            
                proc_info.UseShellExecute = false;
                proc_info.CreateNoWindow  = true;
                proc      = System.Diagnostics.Process.Start(proc_info);
                proc.WaitForExit();
            });
            tsk.Start();
            await tsk;

            if(m_build_cancel) return;

            m_build_progress = 0.5f;
            m_build_step     = "Cleanup";

            //UI wait
            await System.Threading.Tasks.Task.Delay(1500);

            string[] build_files;

            //Delete old files if exists.           
            if(Directory.Exists(docfxBuildTargetPath)) {
                //Debug.Log($"DocumentationTools> Build / Cleaning [{docfxBuildTargetPath}]");
                //Files to ignore deletion.
                List<string> ignore_files = new List<string>(){ 
                    ".git",
                    "license",
                    "cname"
                };
                build_files = Directory.GetFiles(docfxBuildTargetPath,"*", SearchOption.AllDirectories);
                for(int i = 0; i<build_files.Length; i++) {
                    FileInfo fi = new FileInfo(build_files[i]);
                    //Ignore missing
                    if(!fi.Exists) continue;
                    //Ignored files
                    if(ignore_files.Contains(fi.Name.ToLower())) continue;                    
                    fi.Delete();
                }
            }
            else {
                //Debug.Log($"DocumentationTools> Build / Creating [{docfxBuildTargetPath}]");
                //Create if fresh build
                Directory.CreateDirectory(docfxBuildTargetPath);
            }            

            if(m_build_cancel) return;

            m_build_progress = 0.75f;
            m_build_step     = "Moving Files";

            //UI wait
            await System.Threading.Tasks.Task.Delay(1500);

            //Assert Build Temp
            DirectoryInfo build_temp_path_di = new DirectoryInfo(docfxBuildTempPath);
            if (!build_temp_path_di.Exists) build_temp_path_di.Create();
            //Fetch new build files and move them
            build_files = Directory.GetFiles(docfxBuildTempPath,"*", SearchOption.AllDirectories);
            for(int i = 0; i<build_files.Length; i++) {
                string fn = build_files[i];
                //Replace 'build-temp' folder by 'build'
                fn = fn.Replace(docfxBuildTempFolder,docfxBuildTargetFolder);                
                File.Move(build_files[i],fn);
            }
            //Delete empty 'build-temp' folder
            Directory.Delete(docfxBuildTempPath,true);

            m_build_progress = 0.99f;
            m_build_step     = "Complete";

            //UI wait
            await System.Threading.Tasks.Task.Delay(800);

            Debug.Log($"DocumentationTools> Build / Complete!");
            EditorUtility.RevealInFinder(docfxBuildTargetPath);
            m_build_progress = 1f;
            
        }


    }
}