using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;
using Microsoft.CSharp.RuntimeBinder;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace Amisys.Framework.Infrastructure.UserControls
{
    public class AmiDateTimePicker : DXWindow, IComponentConnector
    {
        private DateTime _DateValue;
        private bool _IsOK = false;
        private bool _IsRemove = false;
        internal DateEdit AmiDateEdit;
        internal Button Remove;
        internal Button Cancel;
        internal Button Ok;
        private bool _contentLoaded;

        public DateTime DateValue
        {
            get => this._DateValue;
            set
            {
                this._DateValue = value;
                this.AmiDateEdit.EditValue = value;
            }
        }

        public bool IsOK
        {
            get => this._IsOK;
            set => this._IsOK = value;
        }

        public bool IsRemove
        {
            get => this._IsRemove;
            set => this._IsRemove = value;
        }

        public AmiDateTimePicker() => this.InitializeComponent();

        public AmiDateTimePicker(dynamic view)
        {
            InitializeComponent();
            var pos = Mouse.GetPosition(view as IInputElement);
            var transform = ((UIElement)view).TransformToAncestor(this);
            Point absolutePos = transform.Transform(new Point(0, 0));
            Left = absolutePos.X;
            Top = absolutePos.Y - 50;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (((BaseEdit)this.AmiDateEdit).EditValue != null)
            {
                this.IsOK = true;
                this.DateValue = (DateTime)((BaseEdit)this.AmiDateEdit).EditValue;
            }
          ((Window)this).Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => ((Window)this).Close();

        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            this.IsRemove = true;
            ((Window)this).Close();
        }

        [GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
        [DebuggerNonUserCode]
        public void InitializeComponent()
        {
            if (this._contentLoaded)
                return;
            this._contentLoaded = true;
            Application.LoadComponent((object)this, new Uri("/AmiAFInfrastructure;component/usercontrols/amidatetimepicker.xaml", UriKind.Relative));
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
        [DebuggerNonUserCode]
        void IComponentConnector.Connect(int connectionId, object target)
        {
            switch (connectionId)
            {
                case 1:
                    this.AmiDateEdit = (DateEdit)target;
                    break;
                case 2:
                    this.Remove = (Button)target;
                    this.Remove.Click += new RoutedEventHandler(this.Remove_Click);
                    break;
                case 3:
                    this.Cancel = (Button)target;
                    this.Cancel.Click += new RoutedEventHandler(this.Cancel_Click);
                    break;
                case 4:
                    this.Ok = (Button)target;
                    this.Ok.Click += new RoutedEventHandler(this.Ok_Click);
                    break;
                default:
                    this._contentLoaded = true;
                    break;
            }
        }
    }
}
            
