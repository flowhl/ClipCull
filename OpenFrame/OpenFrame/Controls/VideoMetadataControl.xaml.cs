using OpenFrame.Models;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace OpenFrame.Controls
{
    /// <summary>
    /// UserControl for displaying video metadata
    /// </summary>
    public partial class VideoMetadataControl : UserControl
    {
        #region Dependency Properties

        public static readonly DependencyProperty MetadataProperty =
            DependencyProperty.Register(nameof(Metadata), typeof(VideoMetadata), typeof(VideoMetadataControl),
                new PropertyMetadata(null));

        public VideoMetadata Metadata
        {
            get => (VideoMetadata)GetValue(MetadataProperty);
            set => SetValue(MetadataProperty, value);
        }

        #endregion

        #region Constructor

        public VideoMetadataControl()
        {
            InitializeComponent();
            //bMain.DataContext = this;
        }

        #endregion
    }
}