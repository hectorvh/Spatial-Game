using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Snaps a horizontal ScrollRect to whole pages. A short sideways finger travel turns
/// one page, measured in screen pixels so a phone swipe does not have to cross a large
/// fraction of the card. Uses unscaled time because the level select runs with timeScale at 0.
/// </summary>
[RequireComponent(typeof(ScrollRect))]
public class CarouselPageSnap : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    public float snapTime = 0.18f;

    ScrollRect scroll;
    int pageCount = 1;
    bool dragging;
    bool gestureOpen;
    Vector2 gestureOrigin;
    int gesturePage;
    float velocity;

    public int Page { get; private set; }
    public int PageCount => pageCount;
    public event Action<int> PageChanged;

    /// <summary>Continuous page index, for card scaling between pages.</summary>
    public float PagePosition => pageCount > 1 ? Position / PageStep : 0f;

    float PageStep => pageCount > 1 ? 1f / (pageCount - 1) : 1f;

    float Position
    {
        get => pageCount > 1 ? scroll.horizontalNormalizedPosition : 0f;
        set
        {
            if (pageCount > 1)
            {
                scroll.horizontalNormalizedPosition = value;
            }
        }
    }

    void Awake()
    {
        scroll = GetComponent<ScrollRect>();
    }

    public void Configure(int pages)
    {
        pageCount = Mathf.Max(1, pages);
        Page = Mathf.Clamp(Page, 0, pageCount - 1);
    }

    public void GoTo(int page, bool instant = false)
    {
        page = Mathf.Clamp(page, 0, pageCount - 1);
        bool changed = page != Page;
        Page = page;

        if (instant)
        {
            velocity = 0f;
            Position = page * PageStep;
        }

        if (changed)
        {
            PageChanged?.Invoke(Page);
        }
    }

    public bool IsSwipe(Vector2 travel)
    {
        return Mathf.Abs(travel.x) >= SwipeDistance() && Mathf.Abs(travel.x) >= Mathf.Abs(travel.y);
    }

    /// <summary>About 0.12 inch, capped, so a short thumb swipe turns the page.</summary>
    public static float SwipeDistance()
    {
        float fromDpi = Screen.dpi > 1f ? Screen.dpi * 0.12f : 48f;
        float fromScreen = Mathf.Max(1f, Screen.width) * 0.045f;
        return Mathf.Clamp(Mathf.Min(fromDpi, fromScreen), 24f, 72f);
    }

    public void NotePointerDown(Vector2 screenPosition)
    {
        if (gestureOpen)
        {
            return;
        }

        gestureOpen = true;
        dragging = true;
        gestureOrigin = screenPosition;
        gesturePage = Page;
    }

    public void NotePointerUp(Vector2 screenPosition)
    {
        if (!gestureOpen)
        {
            return;
        }

        gestureOpen = false;
        dragging = false;
        if (scroll != null)
        {
            scroll.velocity = Vector2.zero;
        }

        Vector2 travel = screenPosition - gestureOrigin;
        if (IsSwipe(travel))
        {
            GoTo(gesturePage + (travel.x < 0f ? 1 : -1));
        }
        else
        {
            GoTo(gesturePage);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragging = true;
        NotePointerDown(eventData.pressPosition);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        dragging = false;
        if (scroll != null)
        {
            scroll.velocity = Vector2.zero;
        }

        NotePointerUp(eventData.position);
    }

    void Update()
    {
        Touchscreen screen = Touchscreen.current;
        if (screen == null)
        {
            return;
        }

        UnityEngine.InputSystem.Controls.TouchControl touch = screen.primaryTouch;
        if (touch.press.wasPressedThisFrame)
        {
            NotePointerDown(touch.position.ReadValue());
        }

        if (gestureOpen && touch.press.wasReleasedThisFrame)
        {
            NotePointerUp(touch.position.ReadValue());
        }
    }

    void LateUpdate()
    {
        if (dragging || pageCount < 2)
        {
            return;
        }

        float target = Page * PageStep;
        Position = Mathf.SmoothDamp(Position, target, ref velocity, snapTime, Mathf.Infinity, Time.unscaledDeltaTime);
    }
}

/// <summary>Tap centers a card. A swipe is left to <see cref="CarouselPageSnap"/>.</summary>
public class CarouselCardPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    public CarouselPageSnap snap;
    public int page;

    public void OnPointerDown(PointerEventData eventData)
    {
        snap.NotePointerDown(eventData.position);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        snap.NotePointerUp(eventData.position);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (snap.IsSwipe(eventData.position - eventData.pressPosition))
        {
            return;
        }

        snap.GoTo(page);
    }
}
