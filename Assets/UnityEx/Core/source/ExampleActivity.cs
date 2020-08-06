using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityExt.Core;

namespace UnityEx.Core.Examples {

    /// <summary>
    /// Perform a few examples of activity usage.
    /// </summary>
    public class ExampleActivity : MonoBehaviour {

        #region enum CaseTypeFlag

        /// <summary>
        /// Enumeration to choose an example.
        /// </summary>
        public enum CaseTypeFlag {
            Basic,
            Await,
            Rotation,
            RotationThreaded,
        }

        #endregion

        /// <summary>
        /// Example type.
        /// </summary>
        public CaseTypeFlag  type; 

        /// <summary>
        /// Content holder.
        /// </summary>
        public Transform content;

        /// <summary>
        /// CTOR.
        /// </summary>
        protected void Start() {

            switch(type) {

                #region Basic
                //Simple activity loop rotating a cube
                case CaseTypeFlag.Basic:  {                    
                    Activity.Run("activity-update",       delegate(Activity a) { Debug.Log("ExampleActivity> Run at Update");       }, Activity.Context.Update);
                    Activity.Run("activity-late-update",  delegate(Activity a) { Debug.Log("ExampleActivity> Run at LateUpdate");   }, Activity.Context.LateUpdate);
                    Activity.Run("activity-fixed-update", delegate(Activity a) { Debug.Log("ExampleActivity> Run at FixedUpdate");  }, Activity.Context.FixedUpdate);
                    Activity.Run("activity-async-update", delegate(Activity a) { Debug.Log("ExampleActivity> Run at Update Async"); }, Activity.Context.Async);
                    Activity.Run("activity-thread",       delegate(Activity a) { Debug.Log("ExampleActivity> Run inside a Thread"); }, Activity.Context.Thread);
                }
                break;
                #endregion

                #region Await
                //Simple activity loop rotating a cube
                case CaseTypeFlag.Await:  {
                    float t = 0f;
                    Activity async_a =
                    Activity.Run("activity-simple-loop",
                    delegate(Activity a) { 
                        t+= Time.deltaTime;
                        if(t>=3f) { Debug.Log("ExampleActivity> Await / Loop Complete"); return false; }
                        return true;
                    }, Activity.Context.Update);
                    //Async callback for the example.
                    System.Action async_cb = 
                    async delegate() { 
                        Debug.Log("ExampleActivity> Await / Activity Wait Start");
                        //Start was tagged as 'async'
                        await async_a;
                        Debug.Log("ExampleActivity> Await / Activity Wait Complete");
                    };
                    //Call the method
                    async_cb();                    
                    Debug.Log("ExampleActivity> Await / Before Break");
                    
                }
                break;
                #endregion

                #region Rotation
                //Simple activity loop rotating a cube
                case CaseTypeFlag.Rotation:  {
                    GameObject cube_target = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube_target.transform.parent = content ? content : transform;
                    cube_target.name = "cube";
                    float rotation_angle = 0f;
                    Activity.Run("rotator-activity-example",
                    delegate(Activity a) { 
                        cube_target.transform.localEulerAngles = Vector3.up * rotation_angle;
                        rotation_angle += Time.deltaTime * 90f;
                        return true;
                    }, Activity.Context.Update);
                }
                break;
                #endregion

                #region RotationThreaded
                //Simple activity loop rotating a cube in a thread and applying current state in the main thread.
                case CaseTypeFlag.RotationThreaded:  {
                    GameObject cube_target = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube_target.transform.parent = content ? content : transform;
                    cube_target.name = "cube";
                    float rotation_angle = 0f;                    
                    //Time tracking for threads
                    System.Diagnostics.Stopwatch clk = new System.Diagnostics.Stopwatch();
                    clk.Start();
                    float dt=0f;
                    //Starts the thread to process heavy operation
                    Activity.Run("rotator-activity-thread",
                    delegate(Activity a) {                         
                        float clk_t = ((float)clk.ElapsedMilliseconds);
                        if(clk_t<1f) return true;
                        dt = clk_t/1000f;
                        rotation_angle += dt * 90f;
                        clk.Restart();
                        return true;
                    }, Activity.Context.Thread);
                    //Starts the loop that collects whichever results are in from the thread
                    Activity.Run("rotator-activity-update",
                    delegate(Activity a) { 
                        cube_target.transform.localEulerAngles = Vector3.up * rotation_angle;
                        return true;
                    }, Activity.Context.Update);
                }
                break;
                #endregion

            }

        }

    }

}