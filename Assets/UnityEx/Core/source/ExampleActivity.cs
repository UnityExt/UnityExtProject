using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityExt.Core;

namespace UnityExt.Core.Examples {

    /// <summary>
    /// Perform a few examples of activity usage.
    /// </summary>
    public class ExampleActivity : MonoBehaviour {

        #region class Rotator
        /// <summary>
        /// Base class that can support different interfaces
        /// </summary>
        public class Rotator : ActivityBehaviour {
            /// <summary>
            /// Rotation speed
            /// </summary>
            public Vector3 speed;
            /// <summary>
            /// Current angle
            /// </summary>
            public Vector3 angle;
            /// <summary>
            /// Last time
            /// </summary>
            private double m_last_time;                        
            private Transform m_tcache;
            private bool m_wait;
            static System.Diagnostics.Stopwatch m_clk;
            double clk_time { get { return (((double)m_clk.ElapsedMilliseconds)/1000.0);  } }
            /// <summary>
            /// CTOR
            /// </summary>
            protected void Awake() {
                if(m_clk==null) { m_clk = new System.Diagnostics.Stopwatch(); m_clk.Start(); }
                m_last_time = 0f;
                m_tcache    = transform;
            }
            /// <summary>
            /// Steps the rotation
            /// </summary>
            public void Step() {
                if(m_wait) return;
                double dt = clk_time - m_last_time;                                                                
                m_last_time = clk_time;
                //Super slow stepping
                for(int i=0;i<200;i++) angle += speed * (float)(dt/200.0);
                m_wait = true;
            }
            /// <summary>
            /// Applies the rotation.
            /// </summary>
            public void Apply() {                
                if(m_tcache) m_tcache.localEulerAngles = angle;
                m_wait=false;
            }
        }

        /// <summary>
        /// Rotator that runs inside the unity thread
        /// </summary>
        public class MonoRotator : Rotator, IUpdateable {  public void OnUpdate() { Step(); Apply(); } }

        /// <summary>
        /// Rotator that runs inside a thread and applies the result in the unity thread.
        /// </summary>
        public class ThreadRotator : Rotator, IUpdateable, IThreadUpdateable {  
            public void OnThreadUpdate() { Step();  }
            public void OnUpdate()       { Apply(); } 
        }

        #endregion

        #region enum CaseTypeFlag

        /// <summary>
        /// Enumeration to choose an example.
        /// </summary>
        public enum CaseTypeFlag {
            Basic,
            Await,
            Rotation,
            RotationThreaded,
            RotationBatch,
            RotationBatchThreaded,
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
                    GameObject cube_target = CreatePrimitive(PrimitiveType.Cube,"cube",Vector3.zero,Vector3.one);
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
                    GameObject cube_target = CreatePrimitive(PrimitiveType.Cube,"cube",Vector3.zero,Vector3.one);
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

                #region RotationBatchThreaded + RotationBatch
                case CaseTypeFlag.RotationBatchThreaded:
                case CaseTypeFlag.RotationBatch: {

                    //Init basic layout data
                    int cx = 20;
                    int cz = 20;
                    int cy = 25;
                    int max_cubes = cx*cy*cz;
                    float csm = 2f;
                    //cube size
                    float cube_size = (csm-0.8f)/(float)Mathf.Max(cx,cy,cz);
                    //locals
                    Vector3 p = Vector3.zero;
                    Vector3 s = Vector3.one * cube_size;

                    int create_steps = Mathf.Max(1,(cx*cz)/10);
                    int k=0;

                    //Async create the layout
                    Activity.Run(delegate(Activity a) {
                        for(int i=0;i<create_steps;i++) {                            
                            //cube grid layout
                            p.x = ((float)(k%cx))      * (cube_size*csm);
                            p.z = ((float)(k/cx)%cz)   * (cube_size*csm);
                            p.y = ((float)(k/(cx*cz))) * (cube_size*csm);

                            p.y -= (((float)(cy-1))*0.5f)  * (cube_size*csm);
                        
                            GameObject it = CreatePrimitive(PrimitiveType.Cube,"cube-"+k.ToString("0000"),p,s);
                            Rotator rc = null;
                            //Based on example type add the needed rotator
                            if(type==CaseTypeFlag.RotationBatch)         rc = it.AddComponent<MonoRotator>();
                            if(type==CaseTypeFlag.RotationBatchThreaded) rc = it.AddComponent<ThreadRotator>();
                            //Init random speed
                            if(rc)rc.speed = new Vector3(Random.Range(-5f,5f),Random.Range(-40f,40f),Random.Range(-5f,5f));
                            k++;
                            if(k>=max_cubes) break;
                        }
                        if(k>=max_cubes) return false;
                        return true;
                    });

                    Debug.Log($"ExampleActivity> Created [{max_cubes}] Cubes");

                }
                break;
                #endregion
            }

        }

        /// <summary>
        /// Creates a cube.
        /// </summary>
        /// <param name="p_name"></param>
        /// <param name="p_pos"></param>
        /// <param name="p_scl"></param>
        /// <returns></returns>
        protected GameObject CreatePrimitive(PrimitiveType p_type,string p_name,Vector3 p_pos,Vector3 p_scl) {
            GameObject res = GameObject.CreatePrimitive(p_type);
            res.transform.parent = content ? content : transform;
            res.name = p_name;
            res.transform.localPosition = p_pos;
            res.transform.localScale    = p_scl;
            MeshRenderer mr = res.GetComponent<MeshRenderer>();
            mr.receiveShadows = false;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return res;
        }

    }

}