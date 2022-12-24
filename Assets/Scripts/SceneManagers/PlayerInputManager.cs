using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PlayerInputManager : MonoBehaviour
{


    // menu
    public GameObject MenuGO;

    // camera
    private float targetCameraSize;
    private Vector3 targetCameraPositionWorld;
    private float[] cameraBounds;

    // mouse interaction
    private Vector3 currentMousePositionWorld;
    private Vector3 initialMultiselectMousePosition;
    public GameObject selectionBoxPrefab;
    private GameObject selectionBoxGO;
    private bool mouseIsUIHovered;

    // entity selection
    private GameObject hoveredEntity;
    private List<GameObject> currentEntitiesSelected = new List<GameObject>();
    private IDictionary<int, Vector3> entityIdToMouseOffset;

    // inventory canvas
    public GameObject inventoryCanvas;


    // UNITY HOOKS

    void Start()
    {
        this.MenuGO.SetActive(false);
        this.targetCameraSize = Camera.main.orthographicSize;
        this.targetCameraPositionWorld = Camera.main.transform.position;
        this.cameraBounds = new float[4] { 50, -50, -50, 50 };
        this.selectionBoxGO = Instantiate(this.selectionBoxPrefab, Vector3.zero, Quaternion.identity);
        this.selectionBoxGO.SetActive(false);
    }

    void Update()
    {
        // set state
        this.currentMousePositionWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        // player input
        this.CheckMenuOpen();
        // camera
        if (PlaySceneManager.instance.inputMode != GameSettings.INPUT_MODE_MENU)
        {
            this.HandleCameraMovement();
            this.HandleCameraZoom();
            this.HandleEntityDeleteByKeyDown();
            this.HandleMouseEntityInteraction();
            this.HandleEntityRotation();
        }
    }

    // INTF METHODS

    public void InitEntitySelect()
    {
        foreach (GameObject e in this.currentEntitiesSelected)
        {
            e.GetComponent<Selectable>().SetSelected(false);
        }
        this.currentEntitiesSelected = new List<GameObject>();
        this.entityIdToMouseOffset = new Dictionary<int, Vector3>();
    }

    public void SelectSingleEntity(GameObject entity)
    {
        // Debug.Log("Selecting single entity: " + entity.name);
        this.SelectEntities(new List<GameObject>() { entity });
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        this.entityIdToMouseOffset.Add(entity.GetInstanceID(), entity.transform.position - mousePosition);
    }

    public void DeleteSelectedEntities()
    {
        foreach (GameObject e in this.currentEntitiesSelected)
        {
            Destroy(e);
        }
        this.InitEntitySelect();
    }

    // IMPL METHODS

    private void CheckMenuOpen()
    {
        if (Input.GetKeyDown(GameSettings.MENU_KEY))
        {
            this.MenuGO.SetActive(!this.MenuGO.activeSelf);
            PlaySceneManager.instance.inputMode = this.MenuGO.activeSelf ? GameSettings.INPUT_MODE_MENU : GameSettings.INPUT_MODE_INIT;
        }
    }

    // camera controls
    private void HandleCameraMovement()
    {
        // right click held
        if (Input.GetMouseButton(1))
        {
            // scale camera move amount with size of camera view
            float vert = Input.GetAxis("Mouse Y") * Time.deltaTime * Camera.main.orthographicSize * GameSettings.CAMERA_MOVE_SPEED;
            float horiz = Input.GetAxis("Mouse X") * Time.deltaTime * Camera.main.orthographicSize * GameSettings.CAMERA_MOVE_SPEED;
            Camera.main.transform.Translate(new Vector3(-horiz, -vert, 0));
        }
        // detect mouse at edge of viewport
        else
        {
            float cameraMovePaddingY = Screen.height - (Screen.height * 0.98f);
            float cameraMovePaddingX = Screen.width - (Screen.width * 0.98f);
            Vector3 cameraMoveDirection = Vector3.zero;
            if (Input.mousePosition.y > Screen.height - cameraMovePaddingY)
            {
                cameraMoveDirection += Vector3.up;
            }
            if (Input.mousePosition.y < 0 + cameraMovePaddingY)
            {
                cameraMoveDirection += Vector3.down;
            }
            if (Input.mousePosition.x > Screen.width - cameraMovePaddingX)
            {
                cameraMoveDirection += Vector3.right;
            }
            if (Input.mousePosition.x < 0 + cameraMovePaddingX)
            {
                cameraMoveDirection += Vector3.left;
            }
            if (cameraMoveDirection != Vector3.zero)
            {
                Camera.main.transform.Translate(cameraMoveDirection * Time.deltaTime * Camera.main.orthographicSize * GameSettings.CAMERA_MOVE_SPEED * 0.6f);
            }
        }
        this.ClampCameraToPlayzone();
    }

    private void ClampCameraToPlayzone()
    {
        var p = Camera.main.transform.position;
        Camera.main.transform.position = new Vector3(
            Mathf.Clamp(p.x, this.cameraBounds[1], this.cameraBounds[0]),
            Mathf.Clamp(p.y, this.cameraBounds[2], this.cameraBounds[3]),
            p.z
        );
    }

    private void HandleCameraZoom()
    {
        float currCameraSize = Camera.main.orthographicSize;
        if (Input.mouseScrollDelta.y != 0)
        {
            if (Input.mouseScrollDelta.y > 0)
            {
                this.targetCameraPositionWorld = (Camera.main.ScreenToWorldPoint(Input.mousePosition) + Camera.main.transform.position) / 2;
            }
            else
            {
                this.targetCameraPositionWorld = Camera.main.transform.position;
            }
            this.targetCameraSize = currCameraSize - (Input.mouseScrollDelta.y * GameSettings.CAMERA_ZOOM_AMOUNT);
            // clamp camera size
            if (this.targetCameraSize < GameSettings.CAMERA_SIZE_MIN)
            {
                this.targetCameraSize = GameSettings.CAMERA_SIZE_MIN;
            }
            else if (this.targetCameraSize > GameSettings.CAMERA_SIZE_MAX)
            {
                this.targetCameraSize = GameSettings.CAMERA_SIZE_MAX;
            }
        }
        if (Mathf.Abs(currCameraSize - this.targetCameraSize) > 0.1f)
        {
            Camera.main.orthographicSize = Mathf.Lerp(currCameraSize, this.targetCameraSize, Time.deltaTime * GameSettings.CAMERA_ZOOM_SPEED);
            Camera.main.transform.position = Vector3.Lerp(Camera.main.transform.position, this.targetCameraPositionWorld, Time.deltaTime * (GameSettings.CAMERA_ZOOM_SPEED));
        }
    }

    private void HandleMouseEntityInteraction()
    {
        this.mouseIsUIHovered = this.MouseIsUIHovered();
        Collider2D[] mousePointHits = Physics2D.OverlapPointAll(this.currentMousePositionWorld);
        this.hoveredEntity = this.GetHoveredSelectableEntity(mousePointHits);
        // button down
        if (Input.GetMouseButtonDown(0))
        {
            if (!this.MouseIsUIHovered())
            {
                // entity click
                if (this.hoveredEntity != null)
                {
                    this.HandleEntityClicked();
                }
                // initialize the selection-box
                else
                {
                    this.InitEntitySelect();
                    this.selectionBoxGO.SetActive(true);
                    this.selectionBoxGO.transform.localScale = Vector3.zero;
                    this.initialMultiselectMousePosition = this.currentMousePositionWorld;
                }
            }
        }
        // button held
        else if (Input.GetMouseButton(0))
        {
            // update the position and shape of the selection-box
            if (this.selectionBoxGO.activeSelf)
            {
                Vector3 mPos1 = this.currentMousePositionWorld;
                Vector3 mPos2 = this.initialMultiselectMousePosition;
                float width = Mathf.Abs(mPos1.x - mPos2.x);
                float height = Mathf.Abs(mPos1.y - mPos2.y);
                Vector3 midpoint = (mPos1 - mPos2) / 2;
                this.selectionBoxGO.transform.localScale = new Vector3(width, height, 0);
                Vector3 boxPos = mPos1 - midpoint;
                this.selectionBoxGO.transform.position = new Vector3(boxPos.x, boxPos.y, 0);
            }
            // drag selected entities
            else
            {
                this.HandleEntityDrag();
            }
        }
        // button up
        else if (Input.GetMouseButtonUp(0))
        {
            // box selection
            if (this.selectionBoxGO.activeSelf)
            {
                this.InitEntitySelect();
                this.selectionBoxGO.SetActive(false);
                if (this.currentMousePositionWorld != this.initialMultiselectMousePosition)
                {
                    var entitiesToSelect = new List<GameObject>();
                    Vector3 mPos1 = this.currentMousePositionWorld;
                    Vector3 mPos2 = this.initialMultiselectMousePosition;
                    Collider2D[] selectionBoxHits = Physics2D.OverlapAreaAll(mPos1, mPos2);
                    foreach (Collider2D col in selectionBoxHits)
                    {
                        entitiesToSelect.Add(col.gameObject);
                    }
                    this.SelectEntities(entitiesToSelect);
                }
            }
        }
    }

    private void HandleEntityClicked()
    {
        this.entityIdToMouseOffset = new Dictionary<int, Vector3>();
        // multi entity start drag
        if (hoveredEntity != null && this.currentEntitiesSelected.Count > 0 && this.currentEntitiesSelected.Contains(hoveredEntity))
        {
            // set selected entity initial offsets from mouse position to prepare for entity drag
            foreach (GameObject e in this.currentEntitiesSelected)
            {
                this.entityIdToMouseOffset.Add(e.GetInstanceID(), e.transform.position - this.currentMousePositionWorld);
            }
        }
        // single entity selection
        else if (hoveredEntity != null)
        {
            this.InitEntitySelect();
            this.SelectSingleEntity(hoveredEntity);
        }

    }

    private void HandleEntityDrag()
    {
        foreach (GameObject e in this.currentEntitiesSelected)
        {
            Vector3 offset = this.entityIdToMouseOffset[e.GetInstanceID()];
            e.transform.position = new Vector3(
                this.currentMousePositionWorld.x + offset.x,
                this.currentMousePositionWorld.y + offset.y,
                e.transform.position.z
            );
            if (GameSettings.ENTITY_POSITIONS_DISCRETE)
            {
                e.transform.position = Functions.RoundVector(e.transform.position);
            }
        }
    }

    private void HandleEntityRotation()
    {
        int rot = 0;
        if (Input.GetKeyDown(GameSettings.ROTATE_ENTITIES_LEFT_KEY))
        {
            rot += 90;
        }
        if (Input.GetKeyDown(GameSettings.ROTATE_ENTITIES_RIGHT_KEY))
        {
            rot -= 90;
        }
        if (rot != 0)
        {
            foreach (GameObject e in this.currentEntitiesSelected)
            {
                e.transform.Rotate(new Vector3(0, 0, rot));
            }
        }
    }

    private void HandleEntityDeleteByKeyDown()
    {
        if (this.currentEntitiesSelected.Count > 0 && Input.GetKeyDown(GameSettings.DELETE_ENTITIES_KEY))
        {
            this.DeleteSelectedEntities();
        }
    }

    private bool MouseIsUIHovered()
    {
        List<RaycastResult> raycastResults = new List<RaycastResult>();
        EventSystem es = this.inventoryCanvas.GetComponent<EventSystem>();
        PointerEventData ped = new PointerEventData(es);
        ped.position = Input.mousePosition;
        this.inventoryCanvas.GetComponent<GraphicRaycaster>().Raycast(ped, raycastResults);
        return raycastResults.Count > 0;
    }

    private GameObject GetHoveredSelectableEntity(Collider2D[] hits)
    {
        foreach (Collider2D hit in hits)
        {
            if (hit != null && hit.gameObject.GetComponent<Selectable>() != null)
            {
                return hit.gameObject;
            }
        }
        return null;
    }

    private void SelectEntities(List<GameObject> entities)
    {
        foreach (GameObject entity in entities)
        {
            var selectable = entity.GetComponent<Selectable>();
            if (selectable != null)
            {
                // Debug.Log("Setting entity as selected: " + entity.name);
                this.currentEntitiesSelected.Add(entity);
                selectable.SetSelected(true);
            }
        }
    }


}
