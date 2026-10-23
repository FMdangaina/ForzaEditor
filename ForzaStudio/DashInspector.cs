namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Drawing;
    using System.Drawing.Design;
    using System.Globalization;
    using System.Windows.Forms.Design;

    internal delegate object DashGet();
    internal delegate bool DashSet(object value);

    // The properties of one dash element for a PropertyGrid (like an object inspector): a row per value, with the
    // editor that fits it (drop-down list, number, text, colour). A row without a setter is read only.
    internal class DashInspector : ICustomTypeDescriptor
    {
        // The grid sorts categories by name: leading tabs put them in this order.
        public const string CategoryElement = "\t\t\tElement";
        public const string CategoryTransform = "\t\tTransform";
        public const string CategoryAppearance = "\tAppearance";
        public const string CategoryGraph = "Graph properties";

        private readonly string title;
        private readonly List<PropertyDescriptor> properties = new List<PropertyDescriptor>();

        public DashInspector(string title)
        {
            this.title = title;
        }

        public DashProp Add(string name, string category, string description, Type type, DashGet get, DashSet set)
        {
            DashProp property = new DashProp(name, category, description, type, get, set);
            this.properties.Add(property);
            return property;
        }

        public PropertyDescriptorCollection GetProperties(Attribute[] attributes)
        {
            return new PropertyDescriptorCollection(this.properties.ToArray());
        }

        public PropertyDescriptorCollection GetProperties()
        {
            return this.GetProperties(null);
        }

        public AttributeCollection GetAttributes() { return AttributeCollection.Empty; }
        public string GetClassName() { return this.title; }
        public string GetComponentName() { return this.title; }
        public TypeConverter GetConverter() { return new TypeConverter(); }
        public EventDescriptor GetDefaultEvent() { return null; }
        public PropertyDescriptor GetDefaultProperty() { return null; }
        public object GetEditor(Type editorBaseType) { return null; }
        public EventDescriptorCollection GetEvents(Attribute[] attributes) { return EventDescriptorCollection.Empty; }
        public EventDescriptorCollection GetEvents() { return EventDescriptorCollection.Empty; }
        public object GetPropertyOwner(PropertyDescriptor pd) { return this; }
    }

    internal class DashProp : PropertyDescriptor
    {
        private readonly string category;
        private readonly string description;
        private readonly Type type;
        private readonly DashGet get;
        private readonly DashSet set;
        private TypeConverter converter;
        private UITypeEditor editor;

        public DashProp(string name, string category, string description, Type type, DashGet get, DashSet set)
            : base(name, null)
        {
            this.category = category;
            this.description = description;
            this.type = type;
            this.get = get;
            this.set = set;
        }

        // The values of a drop-down list; exclusive: only those can be chosen.
        public DashProp Choices(string[] values, bool exclusive)
        {
            this.converter = new DashChoiceConverter(values, exclusive);
            return this;
        }

        // The values of a drop-down list built when it opens (fonts, textures); any other text can be typed.
        public DashProp Choices(DashChoices values)
        {
            this.converter = new DashChoiceConverter(values);
            return this;
        }

        public DashProp Color()
        {
            this.editor = new DashColorEditor();
            return this;
        }

        public override AttributeCollection Attributes
        {
            get { return new AttributeCollection(new Attribute[] { new CategoryAttribute(this.category), new DescriptionAttribute(this.description) }); }
        }

        public override TypeConverter Converter
        {
            get { return (this.converter != null) ? this.converter : base.Converter; }
        }

        public override object GetEditor(Type editorBaseType)
        {
            if (this.editor != null && editorBaseType == typeof(UITypeEditor))
            {
                return this.editor;
            }
            return base.GetEditor(editorBaseType);
        }

        public override Type ComponentType { get { return typeof(DashInspector); } }
        public override bool IsReadOnly { get { return this.set == null; } }
        public override Type PropertyType { get { return this.type; } }
        public override bool CanResetValue(object component) { return false; }
        public override void ResetValue(object component) { }
        public override bool ShouldSerializeValue(object component) { return false; }

        public override object GetValue(object component)
        {
            return this.get();
        }

        public override void SetValue(object component, object value)
        {
            if (this.set == null || !this.set(value))
            {
                throw new InvalidOperationException(this.Name + " cannot be changed here.");
            }
        }
    }

    internal delegate string[] DashChoices();

    // Drop-down list of fixed texts (enum-like values) or of texts computed when the list opens.
    internal class DashChoiceConverter : StringConverter
    {
        private readonly string[] fixedValues;
        private readonly DashChoices computed;
        private readonly bool exclusive;

        public DashChoiceConverter(string[] values, bool exclusive)
        {
            this.fixedValues = values;
            this.exclusive = exclusive;
        }

        public DashChoiceConverter(DashChoices values)
        {
            this.computed = values;
        }

        public override bool GetStandardValuesSupported(ITypeDescriptorContext context) { return true; }
        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context) { return this.exclusive; }

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            return new StandardValuesCollection((this.computed != null) ? this.computed() : this.fixedValues);
        }
    }

    // Colour of a text or a model: the dash colour picker in a drop-down, a swatch next to the #RRGGBB text.
    internal class DashColorEditor : UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            return UITypeEditorEditStyle.DropDown;
        }

        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            IWindowsFormsEditorService service = (provider == null) ? null : (IWindowsFormsEditorService) provider.GetService(typeof(IWindowsFormsEditorService));
            if (service == null)
            {
                return value;
            }
            ForzaColorPicker picker = new ForzaColorPicker(Parse(value as string));
            picker.Accepted += delegate { service.CloseDropDown(); };
            picker.Cancelled += delegate { service.CloseDropDown(); };
            service.DropDownControl(picker);
            return picker.IsAccepted ? GameColor.Hex(picker.Value) : value;
        }

        public override bool GetPaintValueSupported(ITypeDescriptorContext context)
        {
            return true;
        }

        public override void PaintValue(PaintValueEventArgs e)
        {
            using (Brush brush = new SolidBrush(Parse(e.Value as string)))
            {
                e.Graphics.FillRectangle(brush, e.Bounds);
            }
        }

        // "#RRGGBB" (the # is optional); anything else is white.
        public static Color Parse(string text)
        {
            int rgb;
            if (text != null && int.TryParse(text.Trim().TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb))
            {
                return Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
            }
            return Color.White;
        }

        public static bool TryParse(string text, out Color color)
        {
            int rgb;
            string digits = (text == null) ? "" : text.Trim().TrimStart('#');
            if (digits.Length == 6 && int.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb))
            {
                color = Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
                return true;
            }
            color = Color.White;
            return false;
        }
    }
}
