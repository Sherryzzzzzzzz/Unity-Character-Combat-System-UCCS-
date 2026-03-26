using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 时间标尺渲染器 — 负责帧/时间标尺的绘制
/// </summary>
public class TimelineRulerRenderer
{
    private const float RULER_HEIGHT = 24f;

    private VisualElement _ruler;
    private AnimationClip _clip;
    private int _totalFrames;
    private System.Action<int> _onFrameClicked;

    public VisualElement RulerElement => _ruler;

    public TimelineRulerRenderer(System.Action<int> onFrameClicked)
    {
        _onFrameClicked = onFrameClicked;

        _ruler = new VisualElement { style = { height = RULER_HEIGHT } };
        _ruler.RegisterCallback<MouseDownEvent>(evt =>
        {
            int frame = ScreenPosToFrame(evt.mousePosition.x);
            _onFrameClicked?.Invoke(frame);
        });
        _ruler.RegisterCallback<GeometryChangedEvent>(evt => DrawTicks());
    }

    public void SetClipData(AnimationClip clip, int totalFrames)
    {
        _clip = clip;
        _totalFrames = totalFrames;
    }

    public void DrawTicks()
    {
        if (_ruler == null || _ruler.resolvedStyle.width <= 1) return;
        _ruler.Clear();

        // 重新创建播放头 handle
        var handle = new VisualElement
        {
            name = "playhead-handle",
            style =
            {
                width = 14,
                height = 14,
                backgroundColor = new Color(0.9f, 0.25f, 0.25f),
                borderTopLeftRadius = 7,
                borderTopRightRadius = 7,
                borderBottomLeftRadius = 7,
                borderBottomRightRadius = 7,
                position = Position.Absolute,
                top = (RULER_HEIGHT - 14) / 2
            }
        };
        _ruler.Add(handle);

        if (_clip == null || _totalFrames <= 0 || _clip.length <= 0) return;

        float w = _ruler.resolvedStyle.width;
        float pixelsPerSec = w / _clip.length;
        float secStep = pixelsPerSec > 100 ? 0.25f : (pixelsPerSec > 40 ? 0.5f : 1.0f);

        for (float t = 0; t < _clip.length; t += secStep / 4)
        {
            bool isMajorTick = Mathf.Approximately(t % secStep, 0);
            bool isMinorTick = !isMajorTick && Mathf.Approximately(t % (secStep / 2), 0);
            if (!isMajorTick && !isMinorTick) continue;

            var tick = new VisualElement
            {
                style =
                {
                    position = Position.Absolute,
                    left = t * pixelsPerSec,
                    width = 1,
                    backgroundColor = isMajorTick ? Color.white : Color.gray,
                    height = isMajorTick ? RULER_HEIGHT * 0.7f : RULER_HEIGHT * 0.4f
                }
            };
            tick.style.top = RULER_HEIGHT - tick.style.height.value.value;
            _ruler.Add(tick);

            if (isMajorTick && pixelsPerSec * secStep > 35)
            {
                _ruler.Add(new Label(t.ToString("F2"))
                {
                    style =
                    {
                        position = Position.Absolute,
                        left = t * pixelsPerSec + 2,
                        top = 2,
                        fontSize = 10
                    }
                });
            }
        }
    }

    public void UpdatePlayheadHandle(int currentFrame)
    {
        if (_ruler == null || _totalFrames <= 0 || _ruler.resolvedStyle.width <= 0) return;
        float pixelsPerFrame = _ruler.resolvedStyle.width / _totalFrames;
        float newLeft = currentFrame * pixelsPerFrame;
        var handle = _ruler.Q(name: "playhead-handle");
        if (handle != null)
            handle.style.left = newLeft - (handle.resolvedStyle.width / 2);
    }

    private int ScreenPosToFrame(float localX)
    {
        if (_clip == null || _totalFrames == 0 || _ruler.resolvedStyle.width <= 0) return 0;
        return Mathf.FloorToInt(Mathf.Clamp01(localX / _ruler.resolvedStyle.width) * _totalFrames);
    }
}
