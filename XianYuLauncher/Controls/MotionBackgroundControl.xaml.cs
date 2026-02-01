using System.Numerics;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;

namespace XianYuLauncher.Controls;

public sealed partial class MotionBackgroundControl : UserControl
{
    private Compositor _compositor;
    private ContainerVisual _rootContainer;
    private readonly Random _random = new Random();
    private readonly List<Visual> _orbs = new List<Visual>();
    private DispatcherTimer _resizeTimer;

    #region Dependency Properties

    public double SpeedRatio
    {
        get => (double)GetValue(SpeedRatioProperty);
        set => SetValue(SpeedRatioProperty, value);
    }
    public static readonly DependencyProperty SpeedRatioProperty =
        DependencyProperty.Register(nameof(SpeedRatio), typeof(double), typeof(MotionBackgroundControl),
            new PropertyMetadata(1.0, OnSpeedRatioChanged));

    public Windows.UI.Color Orb1Color
    {
        get => (Windows.UI.Color)GetValue(Orb1ColorProperty);
        set => SetValue(Orb1ColorProperty, value);
    }
    public static readonly DependencyProperty Orb1ColorProperty =
        DependencyProperty.Register(nameof(Orb1Color), typeof(Windows.UI.Color), typeof(MotionBackgroundControl),
            new PropertyMetadata(Windows.UI.Color.FromArgb(255, 100, 50, 200), (d,e) => ((MotionBackgroundControl)d).UpdateOrbColor(0, (Windows.UI.Color)e.NewValue)));

    public Windows.UI.Color Orb2Color
    {
        get => (Windows.UI.Color)GetValue(Orb2ColorProperty);
        set => SetValue(Orb2ColorProperty, value);
    }
    public static readonly DependencyProperty Orb2ColorProperty =
        DependencyProperty.Register(nameof(Orb2Color), typeof(Windows.UI.Color), typeof(MotionBackgroundControl),
            new PropertyMetadata(Windows.UI.Color.FromArgb(255, 0, 100, 200), (d, e) => ((MotionBackgroundControl)d).UpdateOrbColor(1, (Windows.UI.Color)e.NewValue)));

    public Windows.UI.Color Orb3Color
    {
        get => (Windows.UI.Color)GetValue(Orb3ColorProperty);
        set => SetValue(Orb3ColorProperty, value);
    }
    public static readonly DependencyProperty Orb3ColorProperty =
        DependencyProperty.Register(nameof(Orb3Color), typeof(Windows.UI.Color), typeof(MotionBackgroundControl),
            new PropertyMetadata(Windows.UI.Color.FromArgb(255, 200, 50, 100), (d, e) => ((MotionBackgroundControl)d).UpdateOrbColor(2, (Windows.UI.Color)e.NewValue)));

    public Windows.UI.Color Orb4Color
    {
        get => (Windows.UI.Color)GetValue(Orb4ColorProperty);
        set => SetValue(Orb4ColorProperty, value);
    }
    public static readonly DependencyProperty Orb4ColorProperty =
        DependencyProperty.Register(nameof(Orb4Color), typeof(Windows.UI.Color), typeof(MotionBackgroundControl),
            new PropertyMetadata(Windows.UI.Color.FromArgb(255, 50, 200, 150), (d, e) => ((MotionBackgroundControl)d).UpdateOrbColor(3, (Windows.UI.Color)e.NewValue)));

    public Windows.UI.Color Orb5Color
    {
        get => (Windows.UI.Color)GetValue(Orb5ColorProperty);
        set => SetValue(Orb5ColorProperty, value);
    }
    public static readonly DependencyProperty Orb5ColorProperty =
        DependencyProperty.Register(nameof(Orb5Color), typeof(Windows.UI.Color), typeof(MotionBackgroundControl),
            new PropertyMetadata(Windows.UI.Color.FromArgb(255, 20, 20, 80), (d, e) => ((MotionBackgroundControl)d).UpdateOrbColor(4, (Windows.UI.Color)e.NewValue)));

    #endregion

    private readonly float[] _orbSizes = new[] { 600f, 500f, 550f, 450f, 800f };

    public MotionBackgroundControl()
    {
        this.InitializeComponent();
        
        // 使用 Timer 防抖动，避免调整大小时光球疯狂重置
        _resizeTimer = new DispatcherTimer();
        _resizeTimer.Interval = TimeSpan.FromMilliseconds(500);
        _resizeTimer.Tick += (s, args) => 
        {
            _resizeTimer.Stop();
            UpdateAnimations();
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        InitializeComposition();
        SizeChanged += OnSizeChanged;
        
        // 尝试首次启动动画 (如果窗口已有尺寸)
        // 使用 DispatcherQueue 确保在布局更新后执行
        DispatcherQueue.TryEnqueue(() => 
        {
            if (ActualWidth > 0 && ActualHeight > 0)
            {
                // 停止可能由初始化阶段 SizeChanged 触发的计时器
                // 解决启动 0.5 秒后动画重置导致的位置跳变
                if (_resizeTimer.IsEnabled) _resizeTimer.Stop();

                UpdateAnimations();
            }
        });
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        SizeChanged -= OnSizeChanged;
        _resizeTimer.Stop();
        // 停止并清理
        _rootContainer?.Children.RemoveAll();
        _orbs.Clear();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_rootContainer != null)
        {
            _rootContainer.Size = new Vector2((float)e.NewSize.Width, (float)e.NewSize.Height);
            
            // 防抖处理：只有停止调整大小 500ms 后才重置动画路径
            // 解决 "改0.01px也跳动" 的问题
            if (_resizeTimer.IsEnabled) _resizeTimer.Stop();
             _resizeTimer.Start();
        }
    }

    private void InitializeComposition()
    {
        if (_rootContainer != null && _orbs.Count > 0) return;

        // 1. 获取 Compositor
        _rootContainer = ElementCompositionPreview.GetElementVisual(CompositionHost) as ContainerVisual;
        if (_rootContainer == null)
        {
            var visual = ElementCompositionPreview.GetElementVisual(CompositionHost);
            _compositor = visual.Compositor;
            _rootContainer = _compositor.CreateContainerVisual();
            ElementCompositionPreview.SetElementChildVisual(CompositionHost, _rootContainer);
        }
        else
        {
            _compositor = _rootContainer.Compositor;
        }

        // 确保容器填满画布
        _rootContainer.Size = new Vector2((float)ActualWidth, (float)ActualHeight);

        // 2. 创建光球
        _orbs.Clear();
        
        var colors = new[] { Orb1Color, Orb2Color, Orb3Color, Orb4Color, Orb5Color };
        
        for (int i = 0; i < 5; i++)
        {
            float size = _orbSizes[i];
            var color = colors[i];
            
            var orb = CreateOrb(color, size);
            _rootContainer.Children.InsertAtBottom(orb); // 后面的在下层
            _orbs.Add(orb);
            
            // 初始先给个缩放动画，避免僵硬
            StartScaleAnimation(orb);
        }
    }
    
    // 监听属性变化更新颜色
    private void UpdateOrbColor(int index, Windows.UI.Color newColor)
    {
        if (_orbs == null || index < 0 || index >= _orbs.Count) return;
        
        if (_orbs[index] is SpriteVisual visual && visual.Brush is CompositionRadialGradientBrush brush)
        {
            brush.ColorStops.Clear();
            brush.ColorStops.Add(_compositor.CreateColorGradientStop(0f, newColor));
            brush.ColorStops.Add(_compositor.CreateColorGradientStop(1f, Windows.UI.Color.FromArgb(0, newColor.R, newColor.G, newColor.B)));
        }
    }
    
    private static void OnSpeedRatioChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MotionBackgroundControl control)
        {
            // 防抖
             if (control._resizeTimer != null)
             {
                 if (control._resizeTimer.IsEnabled) control._resizeTimer.Stop();
                 control._resizeTimer.Start();
             }
        }
    }

    private void UpdateAnimations()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        foreach (var orb in _orbs)
        {
            StartFloatingAnimation(orb);
        }
    }

    private SpriteVisual CreateOrb(Windows.UI.Color color, float size)
    {
        var visual = _compositor.CreateSpriteVisual();
        visual.Size = new Vector2(size, size);
        visual.AnchorPoint = new Vector2(0.5f, 0.5f); // 中心对齐

        // 创建径向渐变
        var brush = _compositor.CreateRadialGradientBrush();
        brush.EllipseCenter = new Vector2(0.5f, 0.5f);
        brush.EllipseRadius = new Vector2(0.5f, 0.5f);
        
        // 渐变色：中心不透明 -> 边缘透明
        brush.ColorStops.Add(_compositor.CreateColorGradientStop(0f, color));
        brush.ColorStops.Add(_compositor.CreateColorGradientStop(1f, Windows.UI.Color.FromArgb(0, color.R, color.G, color.B)));

        visual.Brush = brush;
        return visual;
    }

    private void StartScaleAnimation(Visual visual)
    {
         // 动画 2: 呼吸 (Scale) - 独立出来因为不需要依赖窗口尺寸
        var scaleAnim = _compositor.CreateVector3KeyFrameAnimation();
        scaleAnim.Duration = TimeSpan.FromSeconds(_random.Next(5, 10));
        scaleAnim.IterationBehavior = AnimationIterationBehavior.Forever;
        scaleAnim.Direction = AnimationDirection.Alternate;

        float baseScale = 1.0f;
        scaleAnim.InsertKeyFrame(0f, new Vector3(baseScale, baseScale, 1f));
        scaleAnim.InsertKeyFrame(1f, new Vector3(baseScale * 1.5f, baseScale * 1.5f, 1f)); 

        visual.StartAnimation(nameof(Visual.Scale), scaleAnim);
    }

    private void StartFloatingAnimation(Visual visual)
    {
        // 动画 1: 移动 (Offset)
        var offsetAnim = _compositor.CreateVector3KeyFrameAnimation();
        
        // 根据 SpeedRatio 调整速度
        // 默认 15-25s. Speed 越大 Duration 越小
        double baseDuration = _random.Next(15, 25);
        double duration = baseDuration / Math.Max(0.1, SpeedRatio);
        
        offsetAnim.Duration = TimeSpan.FromSeconds(duration); 
        offsetAnim.IterationBehavior = AnimationIterationBehavior.Forever;
        offsetAnim.Direction = AnimationDirection.Alternate; // 来回飘动

        // 随机生成几个关键点
        var viewport = new Vector2((float)ActualWidth, (float)ActualHeight);
        // 稍微扩大一点范围，让球可以飘出去
        float rangeX = viewport.X + 200;
        float rangeY = viewport.Y + 200;

        // 注意：这里每次窗口大小改变都会重置位置序列，可能会有跳变，但背景来说可以接受
        offsetAnim.InsertKeyFrame(0f, GetRandomPosition(rangeX, rangeY));
        offsetAnim.InsertKeyFrame(0.33f, GetRandomPosition(rangeX, rangeY));
        offsetAnim.InsertKeyFrame(0.66f, GetRandomPosition(rangeX, rangeY));
        offsetAnim.InsertKeyFrame(1f, GetRandomPosition(rangeX, rangeY));

        visual.StartAnimation(nameof(Visual.Offset), offsetAnim);
    }

    private Vector3 GetRandomPosition(float width, float height)
    {
        float x = (float)_random.NextDouble() * width - 100;
        float y = (float)_random.NextDouble() * height - 100;
        return new Vector3(x, y, 0);
    }
}
