using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;
using System.Collections;
using System.Windows;
using System.Windows.Input;

namespace Amisys.Framework.Infrastructure.UserControls
{
    public partial class AmiSelectComboBox : DXWindow
    {
        private IEnumerable _dataSource;
        private object _selectedItem;
        private bool _isOK;

        public string WindowTitle { get; set; }

        #region Properties

        public bool IsOK
        {
            get { return _isOK; }
            private set { _isOK = value; }
        }

        public IEnumerable DataSource
        {
            get { return _dataSource; }
            set
            {
                _dataSource = value;
                if (AmiComboBoxEdit != null)
                    AmiComboBoxEdit.ItemsSource = value;
            }
        }

        public object SelectedItem
        {
            get { return _selectedItem; }
            set
            {
                _selectedItem = value;
                if (AmiComboBoxEdit != null && AmiComboBoxEdit.ItemsSource != null)
                    AmiComboBoxEdit.SelectedItem = value;
            }
        }
        public object SelectedItems
        {
            get
            {
                if (AmiComboBoxEdit == null)
                    return null;
                return AmiComboBoxEdit.SelectedItems;
            }
        }

        #endregion

        #region Constructors

        public AmiSelectComboBox()
        {
            WindowTitle = "Selected Item";
            InitializeComponent();
        }

        public AmiSelectComboBox(string title)
            : this()
        {
            WindowTitle = title;
            Title = title;
        }

        public AmiSelectComboBox(string title, object itemsSource)
            : this(title)
        {
            DataBinding(itemsSource);
        }
        public AmiSelectComboBox(string title, object itemsSource, string displayMember)
            : this(title)
        {
            DataBinding(itemsSource, displayMember);
        }

        public AmiSelectComboBox(
            string title,
            object itemsSource,
            string displayMember,
            string valueMember)
            : this(title)
        {
            DataBinding(itemsSource, displayMember, valueMember);
        }

        public AmiSelectComboBox(
            string title,
            object itemsSource,
            string displayMember,
            object view)
            : this(title, itemsSource, displayMember)
        {
            SetLocationFromView(view);
        }

        public AmiSelectComboBox(
            string title,
            object itemsSource,
            string displayMember,
            string valueMember,
            object view)
            : this(title, itemsSource, displayMember, valueMember)
        {
            SetLocationFromView(view);
        }

        #endregion

        #region Binding

        public void DataBinding(object dataSource)
        {
            DataSource = dataSource as IEnumerable;
        }

        public void DataBinding(object dataSource, string displayMember)
        {
            DataSource = dataSource as IEnumerable;
            AmiComboBoxEdit.DisplayMember = displayMember;
        }

        public void DataBinding(object dataSource, string displayMember, string valueMember)
        {
            DataSource = dataSource as IEnumerable;
            AmiComboBoxEdit.DisplayMember = displayMember;
            AmiComboBoxEdit.ValueMember = valueMember;
        }

        #endregion

        #region UI Logic

        public void SetAutoComplete(bool auto)
        {
            if (AmiComboBoxEdit != null)
                AmiComboBoxEdit.AutoComplete = auto;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            IsOK = true;
            SelectedItem = AmiComboBoxEdit.SelectedItem;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            IsOK = false;
            Close();
        }

        private void SetLocationFromView(object view)
        {
            UIElement element = view as UIElement;
            if (element == null)
                return;

            Point mousePos = Mouse.GetPosition(element);
            Point screenPos = element.PointToScreen(mousePos);

            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = screenPos.X;
            Top = screenPos.Y - 50;
        }

        #endregion
    }
}





        
