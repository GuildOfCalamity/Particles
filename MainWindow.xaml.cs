using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Particles
{
    /// <summary>
    /// Animation logic for MainWindow.xaml
    /// Canvas class info --> https://docs.microsoft.com/en-us/dotnet/api/system.windows.controls.canvas?view=netframework-4.8
    /// </summary>
    public partial class MainWindow : Window
    {
        #region [Performance Monitoring]
        private bool showAverage = false;
        private int avgCount = 0;
        private double avgTotal = 0;
        private int avgThresh = 50;
        #endregion [Performance Monitoring]

        #region [Local Class Variables]
        private bool rainbowPalette = false;
        private bool addOutline = true;
        private bool rendering = false;
        private bool animateEnable = true;
        private bool finishedLoad = false;
        private bool isScreenSaver = false;
        private Random rand = new Random();
        private List<OrbInfo> orbs = new List<OrbInfo>();
        DispatcherTimer resetTimer = new DispatcherTimer();
        #endregion [Local Class Variables]


        /// <summary>
        /// Default constructor
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            resetTimer.Interval = TimeSpan.FromSeconds(3);
            resetTimer.Tick += ResetTimer_Tick;

            #region [Delegates]
            // Setup our window resize delegate...
            this.Loaded += (lobj, lea) =>
            {
                /* NOW HANDLING THIS IS OUT RESET TIMER ROUTINE...
                DispatcherTimer timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(1);
                timer.Tick += delegate
                {
                    StartRendering();
                    timer.Stop();
                    timer = null;
                };
                timer.Start();
                */

                var isSCR = System.Reflection.Assembly.GetExecutingAssembly().Location;

                if (isSCR.EndsWith(".scr", StringComparison.CurrentCultureIgnoreCase))
                {
                    isScreenSaver = true;
                    this.Background = new SolidColorBrush(Color.FromArgb(255, 14, 14, 14));
                    this.Cursor = System.Windows.Input.Cursors.None;
                    this.WindowStyle = WindowStyle.None;
                    WindowState = WindowState.Maximized;
                    Topmost = true;
                }
                else
                {
                    isScreenSaver = false;
                    var wnd = lea.Source as Window;
                    WindowInteropHelper WIH = new WindowInteropHelper(wnd);
                    if (this.WindowStyle != WindowStyle.None)
                        Util.UseDarkTitleBar(WIH.Handle); //apply dark theme to title bar
                    this.Cursor = System.Windows.Input.Cursors.Hand;
                    //this.WindowStyle = WindowStyle.ToolWindow;
                    WindowState = WindowState.Normal;
                    wnd.Top = (SystemParameters.WorkArea.Bottom - wnd.Height) / 2;
                    wnd.Left = (SystemParameters.WorkArea.Right - wnd.Width) / 2;
                    Topmost = true;
                }

                finishedLoad = true;
            };

            /** SEE WINDOW EVENTS BELOW **
            // Setup our start animation delegate...
            this.Activated += (aobj, aea) =>
            {
                Topmost = true;
                DispatcherTimer timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(1);
                timer.Tick += delegate
                {
                    StartRendering();
                    timer.Stop();
                    timer = null;
                };
                timer.Start();
            };

            // Setup our stop animation delegate...
            this.Deactivated += (dobj, dea) =>
            {
                Topmost = false;
                StopRendering();
            };
            **/
            #endregion [Delegates]
        }


        #region [Animation Methods]

        /// <summary>
        /// Begin animation routine.
        /// </summary>
        private void StartRendering()
        {
            if (!rendering)
            {
                if (isScreenSaver || this.WindowState == WindowState.Maximized) { Topmost = true; }
                avgCount = 0;
                avgTotal = 0;
                orbs.Clear();
                canvas.Children.Clear();
                CompositionTarget.Rendering += RenderFrame;
                rendering = true;
            }
        }

        /// <summary>
        /// End animation routine.
        /// </summary>
        private void StopRendering()
        {
            if (isScreenSaver || this.WindowState == WindowState.Maximized) { Topmost = false; }
            CompositionTarget.Rendering -= RenderFrame;
            rendering = false;
        }

        // Starting parameters for animation...
        private int ellipseRadius = 20;
        private int minEllipses = 50;
        private int maxEllipses = 100;
        private int minStartingSpeed = 1;
        private int maxStartingSpeed = 10;
        private double speedRatio = 0.1;
        private double accelerationY = 0.03;  // controls the normal change in position per render event (don't go below 0.03)
        private double downCoefecient = 0.1; // 0.01; // controls the acceleration spacing of the falling orbs (0.02)
        private double upCoefecient = 0.01; //0.006;   // controls the acceleration spacing of the rising orbs (0.05)
        /// <summary>
        /// This is our main 'OnPaint' style event from <see cref="System.Windows.Media.CompositionTarget"/>.
        /// Contains a <see cref="Matrix"/>, <see cref="Visual"/>, and a Rendering <see cref="EventHandler"/>.
        /// </summary>
        private void RenderFrame(object sender, EventArgs e)
        {
            if (!animateEnable)
                return;

            /* EXAMPLE USING ANOTHER SHAPE...
            Rectangle rec = new Rectangle();
            rec.Width = 150;
            rec.Height = 150;
            rec.Stroke = new SolidColorBrush(Colors.Black);
            rec.Fill = new SolidColorBrush(Colors.DimGray);
            Canvas.SetLeft(rec, 1);
            Canvas.SetTop(rec, 1);
            canvas.Children.Add(rec);
            */

            if (orbs.Count == 0) // Animation just started, let's create the orbs.
            {   
                if (!isScreenSaver)
                {
                    minEllipses = 50;
                    maxEllipses = 100;
                    ellipseRadius = rand.Next(20, 40); // make the orb sizes normal since we're not full-screen
                }
                else
                {
                    minEllipses = 75;
                    maxEllipses = 125;
                    ellipseRadius = rand.Next(40, 80); // make the orb sizes bigger since we're full-screen
                }

                int halfCanvasWidth = ((int)canvas.ActualWidth / 2) - (ellipseRadius / 2);
                int ellipseCount = rand.Next(minEllipses, maxEllipses + 1);
                for (int i = 0; i < ellipseCount; i++)
                {
                    Ellipse ellipse = new Ellipse();

                    if (addOutline) // helps to identify each orb when clustered
                    {
                        //ellipse.StrokeDashArray = new DoubleCollection() { 1 };
                        ellipse.StrokeThickness = 0.5;
                        ellipse.Stroke = new SolidColorBrush(Color.FromArgb(50, 50, 50, 50));
                    }

                    if (rainbowPalette)
                        ellipse.Fill = CreateRadialGradientBrush();
                    else
                        ellipse.Fill = CreateRadialGradientBrush(Color.FromArgb(110, 0, 255, 0), Color.FromArgb(110, 0, 128, 0)); //CreateGradientBrush(Color.FromArgb(100, 0, 255, 0), Color.FromArgb(100, 0, 128, 0), Color.FromArgb(100, 0, 100, 0));
                    ellipse.Width = ellipseRadius;
                    ellipse.Height = ellipseRadius;
                    // Set the initial start position of the balls...
                    Canvas.SetLeft(ellipse, halfCanvasWidth + rand.Next(-halfCanvasWidth, halfCanvasWidth));
                    Canvas.SetTop(ellipse, 0);
                    canvas.Children.Add(ellipse); 
                    OrbInfo info = new OrbInfo(ellipse, speedRatio * (double)rand.Next(minStartingSpeed, maxStartingSpeed));
                    orbs.Add(info);
                }

                if (showAverage)
                {
                    TextBlock tblk = new TextBlock();
                    tblk.Name = "tbStatus";
                    tblk.Foreground = CreateRadialGradientBrush();
                    tblk.FontSize = 24;
                    Canvas.SetLeft(tblk, canvas.ActualWidth / 4);
                    Canvas.SetTop(tblk, canvas.ActualHeight / 3);
                    canvas.Children.Add(tblk);
                }
            }
            else
            {
                DateTime start = DateTime.UtcNow; // for timing

                // This loop is our non-rendering frame-rate, however long this
                // takes to calculate would be the fastest we could hope to achieve.
                for (int i = orbs.Count - 1; i >= 0; i--)
                {
                    OrbInfo info = orbs[i];
                    double top = Canvas.GetTop(info.Orb);
                    Canvas.SetTop(info.Orb, top + 1 * info.VelocityY);
                    
                    if (!info.Reverse && top >= (canvas.ActualHeight - (ellipseRadius * 1.5)))
                    {
                        info.Reverse = true;
                        // Dampen the stopping motion.
                        info.VelocityY = Math.Min(Math.Min(rand.NextDouble(), rand.NextDouble()), rand.NextDouble());
                        // Create a new color to use
                        info.Orb.Fill = CreateRadialGradientBrush(Color.FromArgb(110, 255, 200, 0), Color.FromArgb(110, 200, 100, 0));
                    }
                    else if (top <= 0 && info.Reverse)
                    {
                        info.Reverse = false;
                        // Dampen the stopping motion.
                        info.VelocityY = Math.Min(Math.Min(rand.NextDouble(), rand.NextDouble()), rand.NextDouble()) * -1.0;
                        // Create a new color to use
                        info.Orb.Fill = CreateRadialGradientBrush(Color.FromArgb(110, 0, 255, 0), Color.FromArgb(110, 0, 128, 0));
                    }
                    else
                    {
                        if (info.Reverse)
                        {   // Decrease the velocity.
                            info.VelocityY -= rand.NextDouble() * upCoefecient; //info.VelocityY -= accelerationY - (rand.NextDouble() * upCoefecient);
                        }
                        else
                        {   // Increase the velocity.
                            info.VelocityY += rand.NextDouble() * downCoefecient; //info.VelocityY += accelerationY + (rand.NextDouble() * downCoefecient);
                        }
                    }

                    if (orbs.Count == 0)
                    {   // End the animation (no work to do)
                        StopRendering();
                    }
                }

                if (showAverage)
                {
                    DateTime end = DateTime.UtcNow;
                    TimeSpan timeDiff = end - start;
                    if (++avgCount >= avgThresh) // wait a bit before calculating average (eleminate startup stutters)
                    {
                        avgTotal += timeDiff.TotalMilliseconds;
                        if (avgCount % 100 == 0) // don't print too much
                        {
                            //System.Diagnostics.Debug.WriteLine($"Average: {(avgTotal / (avgCount - avgThresh)).ToString("0.000")} ms ");
                            FindTextBlock(canvas, $"Update: {(avgTotal / (avgCount - avgThresh)).ToString("0.000")} ms ({orbs.Count})");
                        }
                    }
                }

                
                System.Threading.Thread.Sleep(20); // Adjust this to match frame-rate and your system's processor usage.
                //System.Threading.Thread.Yield(); // Give way to any other processes.
            }
        }

        #endregion [Animation Methods]

        #region [Helpers]
        private void FindTextBlock(Canvas cnvs, string text)
        {
            foreach (FrameworkElement fe in cnvs.Children)
            {
                if (fe is TextBlock tb)
                {
                    tb.Text = text;
                }
                // examples
                /*
                double top1 = (double)fe.GetValue(Canvas.TopProperty);
                double left1 = (double)fe.GetValue(Canvas.LeftProperty);
                double top2 = Canvas.GetTop(fe);
                double left2 = Canvas.GetLeft(fe);
                */
            }
        }

        public static Point PointToWindow(UIElement element, Point pointOnElement)
        {
            Window wnd = Window.GetWindow(element);
            if (wnd == null)
            {
                return new Point(0, 0); // target element is not yet connected to the Window drawing surface
            }
            return element.TransformToAncestor(wnd).Transform(pointOnElement);
        }

        /// <summary>
        /// Brings main window to foreground.
        /// </summary>
        public void BringToForeground()
        {
            if (this.WindowState == WindowState.Minimized || this.Visibility == Visibility.Hidden)
            {
                this.Show();
                this.WindowState = WindowState.Normal;
            }

            // These steps gurantee that an app will be brought to foreground.
            this.Activate();
            this.Topmost = true;
            this.Topmost = false;
            this.Focus();
        }
        #endregion [Helpers]

        #region [System Events]
        private void ResetTimer_Tick(object sender, EventArgs e)
        {
            if (finishedLoad)
            {
                resetTimer.Stop();
                StopRendering();
                StartRendering();
            }
            else
                resetTimer.Stop();
        }

        /// <summary>
        /// This doesn't work well as a delegate in the constructor, so let's create a traditional event for it.
        /// </summary>
        private void particles_Initialized(object sender, EventArgs e)
        {
            if (sender is Window wnd)
                wnd.WindowState = WindowState.Minimized;
        }

        private void particles_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is Window wnd)
            {
                //System.Diagnostics.Debug.WriteLine($"Window width changed: {e.WidthChanged} ");
                //System.Diagnostics.Debug.WriteLine($"Window height changed: {e.HeightChanged} ");
                if (!resetTimer.IsEnabled)
                    resetTimer.Start();
            }
        }

        private void particles_Activated(object sender, EventArgs e)
        {
            //animateEnable = true;
        }

        private void particles_Deactivated(object sender, EventArgs e)
        {
            //animateEnable = false;
        }

        private void particles_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                if (!isScreenSaver)
                    DragMove();
                else
                {
                    StopRendering();
                    this.Cursor = System.Windows.Input.Cursors.Arrow;
                    Close();
                }

            }
            else if (e.RightButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                StopRendering();
                this.Cursor = System.Windows.Input.Cursors.Arrow;
                Close();
            }
            else if (e.MiddleButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                StopRendering();
                StartRendering();
            }
        }

        private void canvas_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                if (!isScreenSaver)
                    DragMove();
                else
                {
                    StopRendering();
                    this.Cursor = System.Windows.Input.Cursors.Arrow;
                    Close();
                }

                /*
                if (e.OriginalSource is Ellipse)
                {
                    Ellipse activeOrb = (Ellipse)e.OriginalSource;
                    activeOrb.Fill = CreateRadialGradientBrush();
                }
                */
            }
            else if (e.RightButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                StopRendering();
                this.Cursor = System.Windows.Input.Cursors.Arrow;
                Close();
                /*
                if (e.OriginalSource is Ellipse)
                {
                    Ellipse activeOrb = (Ellipse)e.OriginalSource;
                    canvas.Children.Remove(activeOrb);
                }
                */
            }
            else if (e.MiddleButton == System.Windows.Input.MouseButtonState.Pressed)
            {
                StopRendering();
                StartRendering();
            }
        }

        #endregion [System Events]


        #region [Brush Helpers]
        /// <summary>
        /// Returns a brush based on user inputs.
        /// </summary>
        /// <returns><see cref="LinearGradientBrush"/></returns>
        public LinearGradientBrush CreateGradientBrush(Color c1, Color c2, Color c3)
        {
            var gs1 = new GradientStop(c1, 0);
            var gs2 = new GradientStop(c2, 0.5);
            var gs3 = new GradientStop(c3, 1);
            var gsc = new GradientStopCollection { gs1, gs2, gs3 };
            var lgb = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = gsc
            };
            return lgb;
        }

        /// <summary>
        /// Returns a brush based on random ranges.
        /// </summary>
        /// <returns><see cref="LinearGradientBrush"/></returns>
        public LinearGradientBrush CreateLinearGradientBrush()
        {
            var gs1 = new GradientStop(Color.FromArgb(128, (byte)rand.Next(50, 255), (byte)rand.Next(50, 255), (byte)rand.Next(50, 255)), 0);
            var gs2 = new GradientStop(Color.FromArgb(128, (byte)rand.Next(50, 255), (byte)rand.Next(50, 255), (byte)rand.Next(50, 255)), 1);
            var gsc = new GradientStopCollection { gs1, gs2 };
            var lgb = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = gsc
            };
            return lgb;
        }

        /// <summary>
        /// Returns a brush based on random ranges.
        /// </summary>
        /// <returns><see cref="RadialGradientBrush"/></returns>
        public RadialGradientBrush CreateRadialGradientBrush()
        {
            var gs1 = new GradientStop(Color.FromArgb(128, (byte)rand.Next(150, 255), (byte)rand.Next(150, 255), (byte)rand.Next(150, 255)), 0);
            var gs2 = new GradientStop(Color.FromArgb(128, (byte)rand.Next(30, 200), (byte)rand.Next(30, 200), (byte)rand.Next(30, 200)), 1);
            var gsc = new GradientStopCollection { gs1, gs2 };
            var rgb = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.3, 0.3), // top-left corner
                GradientStops = gsc
            };
            return rgb;
        }

        /// <summary>
        /// Returns a brush based on random ranges.
        /// </summary>
        /// <returns><see cref="RadialGradientBrush"/></returns>
        public RadialGradientBrush CreateRadialGradientBrush(Color c1, Color c2)
        {
            var gs1 = new GradientStop(c1, 0);
            var gs2 = new GradientStop(c2, 1);
            var gsc = new GradientStopCollection { gs1, gs2 };
            var rgb = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.3, 0.3), // top-left corner
                GradientStops = gsc
            };
            return rgb;
        }
        #endregion [Brush Helpers]

    }

    #region [Ellipse Support Class]
    public class OrbInfo
    {
        public Ellipse Orb { get; set; }
        public double VelocityY { get; set; }
        public bool Reverse { get; set; }

        public OrbInfo(Ellipse ellipse, double velocityY, bool reverse = false)
        {
            VelocityY = velocityY;
            Orb = ellipse;
            Reverse = reverse;
        }
    }
    #endregion [Support Class]
}
