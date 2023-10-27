using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.Networking;

public class PlayerCtr : MonoBehaviour
{
    public Camera cam;
    private Vector3 camDefaultPos;
    private float camDefaultSize, zoomingTime;
    private bool camZoomIn, camZoomStart;
    private float camZoomSize = 25.0f, zoomEndTime = 0.4f;

    public string curLevel = "Level1", nextLevel = "Level2";
    public AnimationCurve dashCurve;
    public AnimationCurve jumpCurve;

    private float horizontalInput, prevHorizontal, dashTime, jumpTime, jumpTimeLimit;
    public float moveSpeed = 10, jumpForce = 250, defaultSpeed, gravity;
    private int jumpCount;
    private bool jumpFinish;
    private bool keyGet = false;
    private Vector3 velocity = Vector3.zero;

    private Rigidbody2D rbody2D;
    float xSpeed, ySpeed;

    private GameObject playerAnim;
    private Animator anim;

    // Metric Manager 
    private MetricManager metricManager;

    void Start()
    {
        camDefaultSize = cam.orthographicSize;
        camDefaultPos = cam.transform.position;
        camZoomIn = false;
        camZoomStart = false;

        rbody2D = GetComponent<Rigidbody2D>();
        defaultSpeed = moveSpeed;
        keyGet = false;
        jumpCount = 0;
        jumpTime = 0.0f;
        jumpTimeLimit = 0.3f;
        jumpFinish = true;

        playerAnim = transform.GetChild(0).gameObject;
        anim = playerAnim.GetComponent<Animator>();

        // Metric Manager Initialization
        metricManager = FindObjectOfType<MetricManager>();
    }

    void FixedUpdate()
    {
        // If the camera is zoomed in, then the player can move
        if (camZoomIn && camZoomStart)
        {
            Vector3 destPos = new Vector3(transform.position.x, transform.position.y, -10.0f);
            // Transform camera position using smooth damp
            cam.transform.position = Vector3.SmoothDamp(cam.transform.position, destPos, ref velocity, 0.2f);
        }
    }

    void Update()
    {
        // Check if space bar is pressed
        bool zoomInOut = Input.GetKeyDown(KeyCode.Space);
        if (zoomInOut)
        {
            // Zoom state is opposite of the current state
            camZoomIn = !camZoomIn;
            zoomingTime = 0.0f;
            camZoomStart = true;
        }
        // If the camera is zoomed in, then the player can move
        if (camZoomIn && camZoomStart)
        {
            // Smoothly zoom in the camera
            zoomingTime += Time.deltaTime;
            cam.orthographicSize = Mathf.Lerp(camDefaultSize, camZoomSize, zoomingTime / zoomEndTime);
            // cam.transform.position = Vector3.Lerp(camDefaultPos, destPos, zoomingTime / zoomEndTime);
            anim.enabled = true;

            // Get player movement input
            horizontalInput = Input.GetAxis("Horizontal");
            bool isJump = Input.GetKey(KeyCode.UpArrow);
            bool isJumpStart = Input.GetKeyDown(KeyCode.UpArrow);
            bool isJumpFin = Input.GetKeyUp(KeyCode.UpArrow);

            // Gradually increase/decrease the horizontal speed of the player
            xSpeed = horizontalInput * moveSpeed;
            if (horizontalInput > 0)
            {
                dashTime += Time.deltaTime;
                playerAnim.transform.localScale = new Vector3(1, 1, 1);
                anim.SetBool("horizontal", true);
            }
            else if (horizontalInput < 0)
            {
                dashTime += Time.deltaTime;
                playerAnim.transform.localScale = new Vector3(-1, 1, 1);
                anim.SetBool("horizontal", true);
            }
            else if (horizontalInput == 0)
            {
                dashTime = 0.0f;
                anim.SetBool("horizontal", false);
            }
            else if (horizontalInput > 0 && prevHorizontal < 0)
            {
                dashTime = 0.0f;
            }
            else if (horizontalInput < 0 && prevHorizontal > 0)
            {
                dashTime = 0.0f;
            }
            prevHorizontal = horizontalInput;
            xSpeed *= dashCurve.Evaluate(dashTime);

            // Allow the player to jump while the jump time is less than the limit
            if (isJumpStart && jumpCount < 1 && jumpTime < jumpTimeLimit)
            {
                jumpTime += Time.deltaTime;
                ySpeed = jumpForce * jumpCurve.Evaluate(jumpTime);
                ++jumpCount;
            }
            else if (!isJumpStart && isJump && jumpTime < jumpTimeLimit)
            {
                jumpTime += Time.deltaTime;
                ySpeed = jumpForce * jumpCurve.Evaluate(jumpTime);
            }
            else
            {
                ySpeed = -gravity;
            }
            if (!jumpFinish)
            {
                jumpFinish = isJumpFin;
            }

            int layer_mask = LayerMask.GetMask(new string[] { "Default" });
            RaycastHit2D hit = Physics2D.Raycast((Vector2)transform.position, -(Vector2)Vector3.up, 3.5f, layer_mask);
            // Debug.DrawRay((Vector2)transform.position, -(Vector2)Vector3.up * 3.5f, Color.red, 100.0f,false);
            if (hit.collider)
            {
                if (jumpFinish)
                {
                    jumpTime = 0;
                    jumpCount = 0;
                    jumpFinish = false;
                }
                moveSpeed = defaultSpeed;
                anim.SetBool("jump", false);
            }
            else
            {
                anim.SetBool("jump", true);
            }

            rbody2D.velocity = new Vector3(xSpeed, ySpeed);
        }
        else if (camZoomStart)
        {
            zoomingTime += Time.deltaTime;
            Vector3 srcPos = new Vector3(transform.position.x, transform.position.y, -10.0f);
            cam.orthographicSize = Mathf.Lerp(camZoomSize, camDefaultSize, zoomingTime / zoomEndTime);
            cam.transform.position = Vector3.Lerp(srcPos, camDefaultPos, zoomingTime / zoomEndTime);
            rbody2D.velocity = new Vector3(0.0f, 0.0f);
            anim.enabled = false;
        }
    }

    public bool IsZoomIn()
    {
        return camZoomIn;
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        if (other.gameObject.CompareTag("Goal") && keyGet)
        {

            Debug.Log(curLevel);
            // Send Analytics when game ends
            metricManager.EndRun();
            string result = metricManager.GetResult(curLevel);
            StartCoroutine(GetRequest(result));

            SceneTransition st = GetComponent<SceneTransition>();
            metricManager.StartRun();
            st.SetLevels(curLevel, nextLevel);
            st.LoadScene();
        }
        else if (other.gameObject.CompareTag("Key"))
        {
            keyGet = true;
            Destroy(other.gameObject);
        }
    }

    private void OnCollisionStay2D(Collision2D other)
    {
        if (other.gameObject.CompareTag("Obstacle"))
        {
            Debug.Log("Collide");
            // rbody2D.velocity = new Vector3(-100, 0);
            transform.position -= new Vector3(1.0f, 0.0f, 0.0f);
        }
    }

    // Get keyGet value
    public bool GetKeyGet()
    {
        return keyGet;
    }

    // Send the analytics to the google form
    IEnumerator GetRequest(string uri)
    {
        using (UnityWebRequest webRequest = UnityWebRequest.Get(uri))
        {
            // Request and wait for the desired page.
            yield return webRequest.SendWebRequest();

            string[] pages = uri.Split('/');
            int page = pages.Length - 1;

            switch (webRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                    Debug.LogError(pages[page] + ": Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    Debug.LogError(pages[page] + ": HTTP Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.Success:
                    Debug.Log(pages[page] + ":\nReceived: " + webRequest.downloadHandler.text);
                    break;
            }
        }
    }
}