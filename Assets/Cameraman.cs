using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class Cameraman : MonoBehaviour
{
    public enum ESTATE
    {
        NORMAL = 0,
        PLANEVIEW,
        SELGRID
    }

    public GameObject showgrid_pos = null;
    public float sensitive_x = 2f;
    public float sensitive_y = 2f;
    public float speed = 5f;
    private Vector3 m_prevpos = Vector3.zero;
    private Quaternion m_prevrot = Quaternion.identity;
    private float rotX = 0;
    private float rotY = 0;
    private float vrotX = 0;
    private float vrotY = 0;
    private float cur_rotx = 0;
    private float cur_roty = 0;

    private Vector3 curpos;

    void Start()
    {
        m_prevpos = this.transform.position;
        m_prevrot = this.transform.rotation;

        this.rotX = this.transform.rotation.x;
        this.rotY = this.transform.rotation.y;
        StartCoroutine(KeyCont());
    }

    private IEnumerator KeyCont()
    {
        while (true)
        {
            curpos.x = Input.GetAxis("Horizontal");
            curpos.y = 0f;
            curpos.z = Input.GetAxis("Vertical");
            
            transform.Translate(curpos);

            yield return null;
        }
    }

    void Update()
    {
        if (Input.GetMouseButton(1))
        {
            this.rotX -= this.sensitive_x * Input.GetAxis("Mouse Y");
            this.rotX = Mathf.Clamp(this.rotX, -90f, 90f);
            this.rotY += this.sensitive_y * Input.GetAxis("Mouse X");

            this.cur_rotx = Mathf.SmoothDamp(this.cur_rotx, this.rotX, ref this.vrotX, 0.1f);
            this.cur_roty = Mathf.SmoothDamp(this.cur_roty, this.rotY, ref this.vrotY, 0.1f);

            this.transform.rotation = Quaternion.Euler(this.cur_rotx, this.cur_roty, 0);
        }

        if (Input.GetKey(KeyCode.Q))
        {
            this.transform.rotation = Quaternion.Euler(0, 0, 0);
        }
    }
}
