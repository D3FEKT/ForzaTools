using System;
using System.Windows.Forms;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using ForzaTools.Bundles.Blobs;

namespace ModelbinParser
{
    public class ModelViewerForm : Form
    {
        private GLControl glControl;
        private BundleParser parser;

        public ModelViewerForm(BundleParser parser)
        {
            this.parser = parser;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "3D Model Viewer";
            this.Width = 800;
            this.Height = 600;

            glControl = new GLControl();
            glControl.Dock = DockStyle.Fill;
            glControl.Load += GlControl_Load;
            glControl.Paint += GlControl_Paint;
            this.Controls.Add(glControl);
        }

        private void GlControl_Load(object sender, EventArgs e)
        {
            GL.ClearColor(System.Drawing.Color.Black);
            GL.Enable(EnableCap.DepthTest);
        }

        private void GlControl_Paint(object sender, PaintEventArgs e)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            // Render the model here using the vertex and face data from the parser
            RenderModel();

            glControl.SwapBuffers();
        }

        private void RenderModel()
        {
            if (parser == null || parser.BundleData == null) return;

            foreach (var blob in parser.BundleData.Blobs)
            {
                if (blob is MeshBlob meshBlob)
                {
                    // Render the meshBlob here
                    GL.Begin(PrimitiveType.Triangles);
                    foreach (var vertex in meshBlob.Vertices)
                    {
                        GL.Vertex3(vertex.Position.X, vertex.Position.Y, vertex.Position.Z);
                    }
                    GL.End();
                }
            }
        }
    }
}
