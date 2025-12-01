using System;
using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;

namespace ForzaTools.ModelBinEditor;

public class HexEditorForm : Form
{
    private HexViewControl hexView;
    private Button btnOk;
    private Button btnCancel;
    private Panel bottomPanel;
    private Label statusLabel;

    [Browsable(false)]
    public byte[] ModifiedData => hexView.Data;

    public HexEditorForm()
    {
        InitializeComponent();
    }

    public HexEditorForm(byte[] data, long startOffset = 0, bool readOnly = false) : this()
    {
        this.Text = $"Hex Editor - Offset: 0x{startOffset:X8} - Length: {data?.Length ?? 0} bytes";

        // Clone data to avoid modifying original until Apply is clicked
        byte[] workingCopy = null;
        if (data != null)
        {
            workingCopy = new byte[data.Length];
            Array.Copy(data, workingCopy, data.Length);
        }

        hexView.Data = workingCopy;
        hexView.StartOffset = startOffset;
        hexView.ReadOnly = readOnly;

        btnOk.Enabled = !readOnly;
        statusLabel.Text = readOnly ? "Read Only" : "Editable";
    }

    private void InitializeComponent()
    {
        this.Size = new Size(800, 600);
        this.StartPosition = FormStartPosition.CenterParent;

        hexView = new HexViewControl();
        hexView.Dock = DockStyle.Fill;
        this.Controls.Add(hexView);

        bottomPanel = new Panel();
        bottomPanel.Height = 45;
        bottomPanel.Dock = DockStyle.Bottom;
        bottomPanel.Padding = new Padding(10);
        bottomPanel.BackColor = SystemColors.Control;

        statusLabel = new Label();
        statusLabel.AutoSize = true;
        statusLabel.Dock = DockStyle.Left;
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        statusLabel.Padding = new Padding(0, 10, 0, 0);

        btnCancel = new Button();
        btnCancel.Text = "Cancel";
        btnCancel.DialogResult = DialogResult.Cancel;
        btnCancel.Dock = DockStyle.Right;

        btnOk = new Button();
        btnOk.Text = "Apply";
        btnOk.DialogResult = DialogResult.OK;
        btnOk.Dock = DockStyle.Right;
        btnOk.Margin = new Padding(0, 0, 10, 0);

        Panel spacer = new Panel();
        spacer.Width = 10;
        spacer.Dock = DockStyle.Right;

        bottomPanel.Controls.Add(statusLabel);
        bottomPanel.Controls.Add(btnOk);
        bottomPanel.Controls.Add(spacer);
        bottomPanel.Controls.Add(btnCancel);

        this.Controls.Add(bottomPanel);
        this.AcceptButton = btnOk;
        this.CancelButton = btnCancel;
    }
}

public class HexViewControl : Control
{
    private VScrollBar vScrollBar;
    private byte[] _data;
    private long _startOffset;
    private int _bytesPerLine = 16;
    private int _lineHeight;
    private int _charWidth;
    private Font _font;

    private long _caretIndex = -1;
    private bool _lowNibble = false;

    [DefaultValue(false)]
    public bool ReadOnly { get; set; } = false;

    // --- FIX FOR DESIGNER ERROR ---
    // These attributes prevent the VS Designer from trying to save the byte array to code
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public byte[] Data
    {
        get => _data;
        set
        {
            _data = value;
            UpdateScroll();
            Invalidate();
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public long StartOffset
    {
        get => _startOffset;
        set
        {
            _startOffset = value;
            Invalidate();
        }
    }
    // -----------------------------

    public HexViewControl()
    {
        this.DoubleBuffered = true;
        this.ResizeRedraw = true;
        this.BackColor = Color.White;
        this.Cursor = Cursors.IBeam;
        _font = new Font("Consolas", 10f);

        vScrollBar = new VScrollBar();
        vScrollBar.Dock = DockStyle.Right;
        vScrollBar.Scroll += (s, e) => Invalidate();
        this.Controls.Add(vScrollBar);
    }

    private void UpdateScroll()
    {
        if (_data == null || _lineHeight == 0) return;

        int totalLines = (int)Math.Ceiling((double)_data.Length / _bytesPerLine);
        int visibleLines = this.ClientSize.Height / _lineHeight;

        if (totalLines > visibleLines)
        {
            vScrollBar.Maximum = totalLines - visibleLines + 2;
            vScrollBar.LargeChange = visibleLines;
            vScrollBar.Enabled = true;
        }
        else
        {
            vScrollBar.Maximum = 0;
            vScrollBar.Enabled = false;
            vScrollBar.Value = 0;
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateScroll();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (vScrollBar.Enabled)
        {
            int newValue = vScrollBar.Value - (e.Delta / 120) * 3;
            vScrollBar.Value = Math.Max(vScrollBar.Minimum, Math.Min(vScrollBar.Maximum - (vScrollBar.LargeChange - 1), newValue));
            Invalidate();
        }
        base.OnMouseWheel(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (_data == null) return;
        this.Focus();

        if (_lineHeight == 0) return;

        int addrWidth = _charWidth * 10;
        int hexStart = addrWidth;
        int hexEnd = hexStart + (_charWidth * 3 * _bytesPerLine);

        int clickedLine = (e.Y / _lineHeight) + vScrollBar.Value;

        if (e.X >= hexStart && e.X < hexEnd)
        {
            int relX = e.X - hexStart;
            int byteIndexOnLine = relX / (_charWidth * 3);
            int charInByte = (relX % (_charWidth * 3)) / _charWidth;

            long index = (long)clickedLine * _bytesPerLine + byteIndexOnLine;

            if (index < _data.Length && index >= 0)
            {
                _caretIndex = index;
                _lowNibble = (charInByte >= 1);
                Invalidate();
            }
        }
    }

    protected override bool IsInputKey(Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Right:
            case Keys.Left:
            case Keys.Up:
            case Keys.Down:
                return true;
        }
        return base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_caretIndex == -1 || _data == null) return;

        if (e.KeyCode == Keys.Right)
        {
            if (!_lowNibble) _lowNibble = true;
            else
            {
                _lowNibble = false;
                if (_caretIndex < _data.Length - 1) _caretIndex++;
            }
        }
        else if (e.KeyCode == Keys.Left)
        {
            if (_lowNibble) _lowNibble = false;
            else
            {
                _lowNibble = true;
                if (_caretIndex > 0) _caretIndex--;
            }
        }
        else if (e.KeyCode == Keys.Down)
        {
            if (_caretIndex + _bytesPerLine < _data.Length) _caretIndex += _bytesPerLine;
        }
        else if (e.KeyCode == Keys.Up)
        {
            if (_caretIndex - _bytesPerLine >= 0) _caretIndex -= _bytesPerLine;
        }

        Invalidate();
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        if (ReadOnly || _caretIndex == -1 || _data == null) return;

        char c = char.ToUpper(e.KeyChar);
        bool isHex = (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F');

        if (isHex)
        {
            int val = (c >= '0' && c <= '9') ? (c - '0') : (c - 'A' + 10);
            byte current = _data[_caretIndex];

            if (!_lowNibble)
            {
                _data[_caretIndex] = (byte)((val << 4) | (current & 0x0F));
                _lowNibble = true;
            }
            else
            {
                _data[_caretIndex] = (byte)((current & 0xF0) | val);
                if (_caretIndex < _data.Length - 1)
                {
                    _caretIndex++;
                    _lowNibble = false;
                }
            }
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_data == null || _data.Length == 0) return;

        Graphics g = e.Graphics;

        if (_lineHeight == 0)
        {
            SizeF size = g.MeasureString("A", _font);
            _charWidth = (int)size.Width;
            _lineHeight = (int)size.Height + 2;
            UpdateScroll();
        }

        int startLine = vScrollBar.Value;
        int visibleLines = (this.ClientSize.Height / _lineHeight) + 1;
        int endLine = Math.Min(startLine + visibleLines, (int)Math.Ceiling((double)_data.Length / _bytesPerLine));

        int y = 0;
        int addrWidth = _charWidth * 10;
        int hexWidth = _charWidth * 3 * _bytesPerLine;

        using (Brush textBrush = new SolidBrush(Color.Black))
        using (Brush offsetBrush = new SolidBrush(Color.Blue))
        using (Brush asciiBrush = new SolidBrush(Color.Gray))
        using (Brush selectionBrush = new SolidBrush(Color.LightBlue))
        {
            for (int i = startLine; i < endLine; i++)
            {
                long offset = i * _bytesPerLine;
                long realAddress = _startOffset + offset;

                g.DrawString(realAddress.ToString("X8"), _font, offsetBrush, 5, y);

                for (int j = 0; j < _bytesPerLine; j++)
                {
                    long index = offset + j;
                    if (index >= _data.Length) break;

                    byte b = _data[index];
                    float hexX = addrWidth + (j * 3 * _charWidth);
                    float asciiX = addrWidth + hexWidth + (_charWidth * 2) + (j * _charWidth);

                    if (index == _caretIndex && !ReadOnly)
                    {
                        float caretX = hexX + (_lowNibble ? _charWidth : 0);
                        g.FillRectangle(selectionBrush, caretX, y, _charWidth, _lineHeight);
                    }

                    g.DrawString(b.ToString("X2"), _font, textBrush, hexX, y);

                    char c = (b >= 32 && b <= 126) ? (char)b : '.';
                    g.DrawString(c.ToString(), _font, asciiBrush, asciiX, y);
                }
                y += _lineHeight;
            }
        }

        g.DrawLine(Pens.LightGray, addrWidth - 5, 0, addrWidth - 5, this.ClientSize.Height);
        g.DrawLine(Pens.LightGray, addrWidth + hexWidth + 5, 0, addrWidth + hexWidth + 5, this.ClientSize.Height);
    }
}