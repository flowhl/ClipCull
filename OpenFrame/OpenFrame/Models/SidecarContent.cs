using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace OpenFrame.Models
{
    public class SidecarContent
    {
        public List<Marker> Markers { get; set; }
        public List<SubClip> SubClips { get; set; }
        public ClipPoint InPoint { get; set; }
        public ClipPoint OutPoint { get; set; }
        public UserMetadataContent UserMetadata { get; set; }
        public SidecarContent()
        {
            Markers = new List<Marker>();
            SubClips = new List<SubClip>();
            UserMetadata = new UserMetadataContent();
        }
    }

    public class UserMetadataContent : INotifyPropertyChanged
    {
        private string _title;
        private string _description;
        private string _author;
        private string _location;
        private string _reel;
        private string _shot;
        private string _camera;
        private int? _rating;
        private bool? _pick;
        private ObservableCollection<Tag> _tags;

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        public string Author
        {
            get => _author;
            set { _author = value; OnPropertyChanged(); }
        }

        public string Location
        {
            get => _location;
            set { _location = value; OnPropertyChanged(); }
        }

        public string Reel
        {
            get => _reel;
            set { _reel = value; OnPropertyChanged(); }
        }

        public string Shot
        {
            get => _shot;
            set { _shot = value; OnPropertyChanged(); }
        }

        public string Camera
        {
            get => _camera;
            set { _camera = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Rating from 1 to 5
        /// </summary>
        public int? Rating
        {
            get => _rating;
            set { _rating = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Pick status, true if picked, false if rejected, null if not set
        /// </summary>
        public bool? Pick
        {
            get => _pick;
            set { _pick = value; OnPropertyChanged(); }
        }

        public ObservableCollection<Tag> Tags
        {
            get => _tags;
            set { _tags = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
