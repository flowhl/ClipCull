using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ClipCull.Controls
{
    /// <summary>
    /// Interaction logic for VideoControlsControl.xaml
    /// </summary>
    public partial class VideoControlsControl : UserControl
    {
        public VideoPreviewControl VideoPreview { get; set; }
        public VideoControlsControl()
        {
            InitializeComponent();
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            VideoPreview?.PlayPauseButton_Click(sender, e);
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            VideoPreview?.StopButton_Click(sender, e);
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            VideoPreview?.VolumeSlider_ValueChanged(sender, e);
        }

        private void FrameBackwardButton_Click(object sender, RoutedEventArgs e)
        {
            VideoPreview?.FrameBackwardButton_Click(sender, e);
        }

        private void FrameForwardButton_Click(object sender, RoutedEventArgs e)
        {
            VideoPreview?.FrameForwardButton_Click(sender, e);
        }

        private void Skip10BackwardButton_Click(object sender, RoutedEventArgs e)
        {
            VideoPreview?.Skip10BackwardButton_Click(sender, e);
        }

        private void Skip10ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            VideoPreview?.Skip10ForwardButton_Click(sender, e);
        }

        private void TimelineControl_TimelineClicked(object sender, TimelineClickedEventArgs e)
        {
            VideoPreview?.TimelineControl_TimelineClicked(sender, e);
        }
    }
}
