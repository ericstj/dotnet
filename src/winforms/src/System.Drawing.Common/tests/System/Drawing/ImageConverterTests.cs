﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Drawing.Imaging;
using System.Globalization;

namespace System.ComponentModel.TypeConverterTests;

public class ImageConverterTest
{
    private readonly Image _image;
    private readonly ImageConverter _imgConv;
    private readonly ImageConverter _imgConvFrmTD;
    private readonly string _imageStr;
    private readonly byte[] _imageBytes;

    public ImageConverterTest()
    {
        _image = Image.FromFile(Path.Join("bitmaps", "TestImage.bmp"));
        _imageStr = _image.ToString();

        using (MemoryStream destStream = new())
        {
            _image.Save(destStream, _image.RawFormat);
            _imageBytes = destStream.ToArray();
        }

        _imgConv = new ImageConverter();
        _imgConvFrmTD = (ImageConverter)TypeDescriptor.GetConverter(_image);
    }

    [Theory]
    [InlineData("48x48_multiple_entries_4bit.ico")]
    [InlineData("256x256_seven_entries_multiple_bits.ico")]
    [InlineData("pngwithheight_icon.ico")]
    public void ImageConverterFromIconTest(string name)
    {
        using Icon icon = new(Helpers.GetTestBitmapPath(name));
        Bitmap IconBitmap = (Bitmap)_imgConv.ConvertFrom(icon);
        Assert.NotNull(IconBitmap);
        Assert.Equal(32, IconBitmap.Width);
        Assert.Equal(32, IconBitmap.Height);
        Assert.Equal(new Size(32, 32), IconBitmap.Size);
    }

    [Fact]
    public void ImageWithOleHeader()
    {
        string path = Path.Join("bitmaps", "TestImageWithOleHeader.bmp");
        using FileStream fileStream = File.Open(path, FileMode.Open);
        using MemoryStream ms = new();
        fileStream.CopyTo(ms);
        ImageConverter converter = new();
        object image = converter.ConvertFrom(ms.ToArray());
        Assert.NotNull(image);
    }

    [Fact]
    public void TestCanConvertFrom()
    {
        Assert.True(_imgConv.CanConvertFrom(typeof(byte[])), "byte[] (no context)");
        Assert.True(_imgConv.CanConvertFrom(null, typeof(byte[])), "byte[]");
        Assert.True(_imgConv.CanConvertFrom(null, _imageBytes.GetType()), "_imageBytes.GetType()");
        Assert.True(_imgConv.CanConvertFrom(typeof(Icon)), "Icon (no context)");
        Assert.True(_imgConv.CanConvertFrom(null, typeof(Icon)), "Icon");
        Assert.False(_imgConv.CanConvertFrom(null, typeof(string)), "string");
        Assert.False(_imgConv.CanConvertFrom(null, typeof(Rectangle)), "Rectangle");
        Assert.False(_imgConv.CanConvertFrom(null, typeof(Point)), "Point");
        Assert.False(_imgConv.CanConvertFrom(null, typeof(PointF)), "PointF");
        Assert.False(_imgConv.CanConvertFrom(null, typeof(Size)), "Size");
        Assert.False(_imgConv.CanConvertFrom(null, typeof(SizeF)), "SizeF");
        Assert.False(_imgConv.CanConvertFrom(null, typeof(object)), "object");
        Assert.False(_imgConv.CanConvertFrom(null, typeof(int)), "int");
        Assert.False(_imgConv.CanConvertFrom(null, typeof(Metafile)), "Mefafile");

        Assert.True(_imgConvFrmTD.CanConvertFrom(typeof(byte[])), "TD byte[] (no context)");
        Assert.True(_imgConvFrmTD.CanConvertFrom(null, typeof(byte[])), "TD byte[]");
        Assert.True(_imgConvFrmTD.CanConvertFrom(null, _imageBytes.GetType()), "TD _imageBytes.GetType()");
        Assert.False(_imgConvFrmTD.CanConvertFrom(null, typeof(string)), "TD string");
        Assert.False(_imgConvFrmTD.CanConvertFrom(null, typeof(Rectangle)), "TD Rectangle");
        Assert.False(_imgConvFrmTD.CanConvertFrom(null, typeof(Point)), "TD Point");
        Assert.False(_imgConvFrmTD.CanConvertFrom(null, typeof(PointF)), "TD PointF");
        Assert.False(_imgConvFrmTD.CanConvertFrom(null, typeof(Size)), "TD Size");
        Assert.False(_imgConvFrmTD.CanConvertFrom(null, typeof(SizeF)), "TD SizeF");
        Assert.False(_imgConvFrmTD.CanConvertFrom(null, typeof(object)), "TD object");
        Assert.False(_imgConvFrmTD.CanConvertFrom(null, typeof(int)), "TD int");
        Assert.False(_imgConvFrmTD.CanConvertFrom(null, typeof(Metafile)), "TD Metafile");
    }

    [Fact]
    public void TestCanConvertTo()
    {
        Assert.True(_imgConv.CanConvertTo(typeof(string)), "stirng (no context)");
        Assert.True(_imgConv.CanConvertTo(null, typeof(string)), "string");
        Assert.True(_imgConv.CanConvertTo(null, _imageStr.GetType()), "_imageStr.GetType()");
        Assert.True(_imgConv.CanConvertTo(typeof(byte[])), "byte[] (no context)");
        Assert.True(_imgConv.CanConvertTo(null, typeof(byte[])), "byte[]");
        Assert.True(_imgConv.CanConvertTo(null, _imageBytes.GetType()), "_imageBytes.GetType()");
        Assert.False(_imgConv.CanConvertTo(null, typeof(Rectangle)), "Rectangle");
        Assert.False(_imgConv.CanConvertTo(null, typeof(Point)), "Point");
        Assert.False(_imgConv.CanConvertTo(null, typeof(PointF)), "PointF");
        Assert.False(_imgConv.CanConvertTo(null, typeof(Size)), "Size");
        Assert.False(_imgConv.CanConvertTo(null, typeof(SizeF)), "SizeF");
        Assert.False(_imgConv.CanConvertTo(null, typeof(object)), "object");
        Assert.False(_imgConv.CanConvertTo(null, typeof(int)), "int");

        Assert.True(_imgConvFrmTD.CanConvertTo(typeof(string)), "TD string (no context)");
        Assert.True(_imgConvFrmTD.CanConvertTo(null, typeof(string)), "TD string");
        Assert.True(_imgConvFrmTD.CanConvertTo(null, _imageStr.GetType()), "TD _imageStr.GetType()");
        Assert.True(_imgConvFrmTD.CanConvertTo(typeof(byte[])), "TD byte[] (no context)");
        Assert.True(_imgConvFrmTD.CanConvertTo(null, typeof(byte[])), "TD byte[]");
        Assert.True(_imgConvFrmTD.CanConvertTo(null, _imageBytes.GetType()), "TD _imageBytes.GetType()");
        Assert.False(_imgConvFrmTD.CanConvertTo(null, typeof(Rectangle)), "TD Rectangle");
        Assert.False(_imgConvFrmTD.CanConvertTo(null, typeof(Point)), "TD Point");
        Assert.False(_imgConvFrmTD.CanConvertTo(null, typeof(PointF)), "TD PointF");
        Assert.False(_imgConvFrmTD.CanConvertTo(null, typeof(Size)), "TD Size");
        Assert.False(_imgConvFrmTD.CanConvertTo(null, typeof(SizeF)), "TD SizeF");
        Assert.False(_imgConvFrmTD.CanConvertTo(null, typeof(object)), "TD object");
        Assert.False(_imgConvFrmTD.CanConvertTo(null, typeof(int)), "TD int");
    }

    [Fact]
    public void ConvertFrom()
    {
        Image newImage = (Image)_imgConv.ConvertFrom(null, CultureInfo.InvariantCulture, _imageBytes);

        Assert.Equal(_image.Height, newImage.Height);
        Assert.Equal(_image.Width, newImage.Width);

        newImage = (Image)_imgConvFrmTD.ConvertFrom(null, CultureInfo.InvariantCulture, _imageBytes);

        Assert.Equal(_image.Height, newImage.Height);
        Assert.Equal(_image.Width, newImage.Width);
    }

    [Fact]
    public void ConvertFrom_ThrowsNotSupportedException()
    {
        Assert.Throws<NotSupportedException>(() => _imgConv.ConvertFrom("System.Drawing.String"));
        Assert.Throws<NotSupportedException>(() => _imgConv.ConvertFrom(null, CultureInfo.InvariantCulture, "System.Drawing.String"));
        Assert.Throws<NotSupportedException>(() => _imgConv.ConvertFrom(null, CultureInfo.InvariantCulture, new Bitmap(20, 20)));
        Assert.Throws<NotSupportedException>(() => _imgConv.ConvertFrom(null, CultureInfo.InvariantCulture, new Point(10, 10)));
        Assert.Throws<NotSupportedException>(() => _imgConv.ConvertFrom(null, CultureInfo.InvariantCulture, new SizeF(10, 10)));
        Assert.Throws<NotSupportedException>(() => _imgConv.ConvertFrom(null, CultureInfo.InvariantCulture, new object()));

        Assert.Throws<NotSupportedException>(() => _imgConvFrmTD.ConvertFrom("System.Drawing.String"));
        Assert.Throws<NotSupportedException>(() => _imgConvFrmTD.ConvertFrom(null, CultureInfo.InvariantCulture, "System.Drawing.String"));
        Assert.Throws<NotSupportedException>(() => _imgConvFrmTD.ConvertFrom(null, CultureInfo.InvariantCulture, new Bitmap(20, 20)));
        Assert.Throws<NotSupportedException>(() => _imgConvFrmTD.ConvertFrom(null, CultureInfo.InvariantCulture, new Point(10, 10)));
        Assert.Throws<NotSupportedException>(() => _imgConvFrmTD.ConvertFrom(null, CultureInfo.InvariantCulture, new SizeF(10, 10)));
        Assert.Throws<NotSupportedException>(() => _imgConvFrmTD.ConvertFrom(null, CultureInfo.InvariantCulture, new object()));
    }

    [Fact]
    public void ConvertTo_String()
    {
        Assert.Equal(_imageStr, (string)_imgConv.ConvertTo(null, CultureInfo.InvariantCulture, _image, typeof(string)));
        Assert.Equal(_imageStr, (string)_imgConv.ConvertTo(_image, typeof(string)));
        Assert.Equal(_imageStr, (string)_imgConvFrmTD.ConvertTo(null, CultureInfo.InvariantCulture, _image, typeof(string)));
        Assert.Equal(_imageStr, (string)_imgConvFrmTD.ConvertTo(_image, typeof(string)));

        using (new ThreadCultureChange(CultureInfo.CreateSpecificCulture("fr-FR"), CultureInfo.InvariantCulture))
        {
            Assert.Equal("(none)", (string)_imgConv.ConvertTo(null, typeof(string)));
            Assert.Equal("(none)", (string)_imgConv.ConvertTo(null, CultureInfo.CreateSpecificCulture("ru-RU"), null, typeof(string)));

            Assert.Equal("(none)", (string)_imgConvFrmTD.ConvertTo(null, typeof(string)));
            Assert.Equal("(none)", (string)_imgConvFrmTD.ConvertTo(null, CultureInfo.CreateSpecificCulture("de-DE"), null, typeof(string)));
        }
    }

    [Fact]
    public void ConvertTo_ByteArray()
    {
        byte[] newImageBytes = (byte[])_imgConv.ConvertTo(null, CultureInfo.InvariantCulture, _image, _imageBytes.GetType());
        Assert.Equal(_imageBytes, newImageBytes);

        newImageBytes = (byte[])_imgConvFrmTD.ConvertTo(null, CultureInfo.InvariantCulture, _image, _imageBytes.GetType());
        Assert.Equal(_imageBytes, newImageBytes);

        newImageBytes = (byte[])_imgConvFrmTD.ConvertTo(_image, _imageBytes.GetType());
        Assert.Equal(_imageBytes, newImageBytes);
    }

    [Fact]
    public void ConvertTo_FromBitmapToByteArray()
    {
        Bitmap value = new(64, 64);
        ImageConverter converter = new();
        byte[] converted = (byte[])converter.ConvertTo(value, typeof(byte[]));
        Assert.NotNull(converted);
    }

    [Fact]
    public void ConvertTo_ThrowsNotSupportedException()
    {
        Assert.Throws<NotSupportedException>(() => _imgConv.ConvertTo(null, CultureInfo.InvariantCulture, _image, typeof(Rectangle)));
        Assert.Throws<NotSupportedException>(() => _imgConv.ConvertTo(null, CultureInfo.InvariantCulture, _image, _image.GetType()));
        Assert.Throws<NotSupportedException>(() => _imgConv.ConvertTo(null, CultureInfo.InvariantCulture, _image, typeof(Size)));
        Assert.Throws<NotSupportedException>(() => _imgConv.ConvertTo(null, CultureInfo.InvariantCulture, _image, typeof(Bitmap)));
        Assert.Throws<NotSupportedException>(() => _imgConv.ConvertTo(null, CultureInfo.InvariantCulture, _image, typeof(Point)));
        Assert.Throws<NotSupportedException>(() => _imgConv.ConvertTo(null, CultureInfo.InvariantCulture, _image, typeof(Metafile)));
        Assert.Throws<NotSupportedException>(() => _imgConv.ConvertTo(null, CultureInfo.InvariantCulture, _image, typeof(object)));
        Assert.Throws<NotSupportedException>(() => _imgConv.ConvertTo(null, CultureInfo.InvariantCulture, _image, typeof(int)));

        Assert.Throws<NotSupportedException>(() => _imgConvFrmTD.ConvertTo(null, CultureInfo.InvariantCulture, _image, typeof(Rectangle)));
        Assert.Throws<NotSupportedException>(() => _imgConvFrmTD.ConvertTo(null, CultureInfo.InvariantCulture, _image, _image.GetType()));
        Assert.Throws<NotSupportedException>(() => _imgConvFrmTD.ConvertTo(null, CultureInfo.InvariantCulture, _image, typeof(Size)));
        Assert.Throws<NotSupportedException>(() => _imgConvFrmTD.ConvertTo(null, CultureInfo.InvariantCulture, _image, typeof(Bitmap)));
        Assert.Throws<NotSupportedException>(() => _imgConvFrmTD.ConvertTo(null, CultureInfo.InvariantCulture, _image, typeof(Point)));
        Assert.Throws<NotSupportedException>(() => _imgConvFrmTD.ConvertTo(null, CultureInfo.InvariantCulture, _image, typeof(Metafile)));
        Assert.Throws<NotSupportedException>(() => _imgConvFrmTD.ConvertTo(null, CultureInfo.InvariantCulture, _image, typeof(object)));
        Assert.Throws<NotSupportedException>(() => _imgConvFrmTD.ConvertTo(null, CultureInfo.InvariantCulture, _image, typeof(int)));
    }

    [Fact]
    public void TestGetPropertiesSupported()
    {
        Assert.True(_imgConv.GetPropertiesSupported(), "GetPropertiesSupported()");
        Assert.True(_imgConv.GetPropertiesSupported(null), "GetPropertiesSupported(null)");
    }

    [Fact]
    public void TestGetProperties()
    {
        const int allPropertiesCount = 14; // Count of all properties in Image class.
        const int browsablePropertiesCount = 7; // Count of browsable properties in Image class (BrowsableAttribute.Yes).

        PropertyDescriptorCollection propsColl;

        // Internally calls TypeDescriptor.GetProperties(typeof(Image), null), which returns all properties.
        propsColl = _imgConv.GetProperties(null, _image, null);
        Assert.Equal(allPropertiesCount, propsColl.Count);

        // Internally calls TypeDescriptor.GetProperties(typeof(Image), new Attribute[] { BrowsableAttribute.Yes }).
        propsColl = _imgConv.GetProperties(null, _image);
        Assert.Equal(browsablePropertiesCount, propsColl.Count);
        propsColl = _imgConv.GetProperties(_image);
        Assert.Equal(browsablePropertiesCount, propsColl.Count);

        // Returns all properties of Image class.
        propsColl = TypeDescriptor.GetProperties(typeof(Image));
        Assert.Equal(allPropertiesCount, propsColl.Count);

        // Internally calls TypeDescriptor.GetProperties(typeof(Image), null), which returns all properties.
        propsColl = _imgConvFrmTD.GetProperties(null, _image, null);
        Assert.Equal(allPropertiesCount, propsColl.Count);

        // Internally calls TypeDescriptor.GetProperties(typeof(Image), new Attribute[] { BrowsableAttribute.Yes }).
        propsColl = _imgConvFrmTD.GetProperties(null, _image);
        Assert.Equal(browsablePropertiesCount, propsColl.Count);
        propsColl = _imgConvFrmTD.GetProperties(_image);
        Assert.Equal(browsablePropertiesCount, propsColl.Count);
    }
}
