/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using OpenMetaverse;
using OpenMetaverse.Assets;
using OpenMetaverse.Imaging;
using OpenMetaverse.Rendering;
using OpenMetaverse.StructuredData;

using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Server.Base;

using Warp3D;
using WarpRenderer = Warp3D.Warp3D;

using CSJ2K;

namespace OpenSim.Region.CoreModules.World.Warp3DMap
{
    public class Warp3DImageModule : IMapImageGenerator, INonSharedRegionModule
    {
        private static readonly Color4 WATER_COLOR = new Color4(29, 72, 96, 216);
//        private static readonly Color4 WATER_COLOR = new Color4(29, 72, 96, 128);

        private static ILogger? m_logger;

#pragma warning disable 414
        private static string LogHeader = "[WARP 3D IMAGE MODULE]";
#pragma warning restore 414

        internal Scene m_scene;
        private IRendering m_primMesher;
        internal IJ2KDecoder m_imgDecoder;

        // caches per rendering 
        private Dictionary<UUID, warp_Texture> m_warpTextures = new Dictionary<UUID, warp_Texture>();
        private Dictionary<UUID, int> m_colors = new Dictionary<UUID, int>();

        private bool m_drawPrimVolume = true;   // true if should render the prims on the tile
        private bool m_textureTerrain = true;   // true if to create terrain splatting texture
        private bool m_textureAverageTerrain = false; // replace terrain textures by their average color
        private bool m_texturePrims = true;     // true if should texture the rendered prims
        private float m_texturePrimSize = 48f;  // size of prim before we consider texturing it
        private bool m_renderMeshes = false;    // true if to render meshes rather than just bounding boxes

        private const float m_cameraHeight = 4096f;
        private float m_renderMinHeight = -100f;
        private float m_renderMaxHeight = 4096f;

        private bool m_Enabled = false;

        #region Region Module interface

        public void Initialise(IConfiguration source)
        {
            m_logger ??= OpenSimServer.Instance.ServiceProvider.GetRequiredService<ILogger<Warp3DImageModule>>();
            string[] configSections = new string[] { "Map", "Startup" };

            if (Util.GetConfigVarFromSections<string>(
                source, "MapImageModule", configSections, "MapImageModule") != "Warp3DImageModule")
                return;

            m_Enabled = true;

            m_drawPrimVolume =
                Util.GetConfigVarFromSections<bool>(source, "DrawPrimOnMapTile", configSections, m_drawPrimVolume);
            m_textureTerrain =
                Util.GetConfigVarFromSections<bool>(source, "TextureOnMapTile", configSections, m_textureTerrain);
            m_textureAverageTerrain =
                Util.GetConfigVarFromSections<bool>(source, "AverageTextureColorOnMapTile", configSections, m_textureAverageTerrain);
            if (m_textureAverageTerrain)
                m_textureTerrain = true;
            m_texturePrims =
                Util.GetConfigVarFromSections<bool>(source, "TexturePrims", configSections, m_texturePrims);
            m_texturePrimSize =
                Util.GetConfigVarFromSections<float>(source, "TexturePrimSize", configSections, m_texturePrimSize);
            m_renderMeshes =
                Util.GetConfigVarFromSections<bool>(source, "RenderMeshes", configSections, m_renderMeshes);

            m_renderMaxHeight = Util.GetConfigVarFromSections<float>(source, "RenderMaxHeight", configSections, m_renderMaxHeight);
            m_renderMinHeight = Util.GetConfigVarFromSections<float>(source, "RenderMinHeight", configSections, m_renderMinHeight);
            /*
            m_cameraHeight = Util.GetConfigVarFromSections<float>(m_config, "RenderCameraHeight", configSections, m_cameraHeight);

            if (m_cameraHeight < 250f)
                m_cameraHeight = 250f;
            else if (m_cameraHeight > 4096f)
                m_cameraHeight = 4096f;
            */
            if (m_renderMaxHeight < 100f)
                m_renderMaxHeight = 100f;
            else if (m_renderMaxHeight > m_cameraHeight - 10f)
                m_renderMaxHeight = m_cameraHeight - 10f;

            if (m_renderMinHeight < -100f)
                m_renderMinHeight = -100f;
            else if (m_renderMinHeight > m_renderMaxHeight - 10f)
                m_renderMinHeight = m_renderMaxHeight - 10f;
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_scene = scene;

            List<string> renderers = RenderingLoader.ListRenderers(Util.ExecutingDirectory());
            if (renderers.Count > 0)
                m_logger?.LogInformation("[MAPTILE]: Loaded prim mesher " + renderers[0]);
            else
                m_logger?.LogInformation("[MAPTILE]: No prim mesher loaded, prim rendering will be disabled");

            m_scene.RegisterModuleInterface<IMapImageGenerator>(this);
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_imgDecoder = m_scene.RequestModuleInterface<IJ2KDecoder>();
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "Warp3DImageModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        #region IMapImageGenerator Members

        private Vector3 cameraPos;
        private Vector3 cameraDir;
        private int viewWidth = 256;
        private int viewHeight = 256;
        private float fov;
        private bool orto;

        public Bitmap CreateMapTile()
        {
            List<string> renderers = RenderingLoader.ListRenderers(Util.ExecutingDirectory());
            if (renderers.Count > 0)
            {
                m_primMesher = RenderingLoader.LoadRenderer(renderers[0]);
            }

            viewWidth = (int)m_scene.RegionInfo.RegionSizeX;
            viewHeight = (int)m_scene.RegionInfo.RegionSizeY;

            cameraPos = new Vector3(
                            viewWidth * 0.5f,
                            viewHeight * 0.5f,
                            m_cameraHeight);

            cameraDir = -Vector3.UnitZ;
            orto = true;

            Bitmap tile = GenImage();
            // image may be reloaded elsewhere, so no compression format
            string filename = "MAP-" + m_scene.RegionInfo.RegionID.ToString() + ".png";
            tile.Save(filename,ImageFormat.Png);
            m_primMesher = null;
            return tile;
        }

        public Bitmap CreateViewImage(Vector3 camPos, Vector3 camDir, float pfov, int width, int height, bool useTextures)
        {
            List<string> renderers = RenderingLoader.ListRenderers(Util.ExecutingDirectory());
            if (renderers.Count > 0)
            {
                m_primMesher = RenderingLoader.LoadRenderer(renderers[0]);
            }

            cameraPos = camPos;
            cameraDir = camDir;
            viewWidth = width;
            viewHeight = height;
            fov = pfov;
            orto = false;

            Bitmap tile = GenImage();
            m_primMesher = null;
            return tile;
        }

        private Bitmap GenImage()
        {
            m_colors.Clear();
            m_warpTextures.Clear();

            WarpRenderer renderer = new WarpRenderer();

            if (!renderer.CreateScene(viewWidth, viewHeight))
                return new Bitmap(viewWidth, viewHeight);

            #region Camera

            warp_Vector pos = ConvertVector(cameraPos);
            warp_Vector lookat = warp_Vector.add(pos, ConvertVector(cameraDir));

            if (orto)
                renderer.Scene.defaultCamera.setOrthographic(true, viewWidth, viewHeight);
            else
                renderer.Scene.defaultCamera.setFov(fov);

            renderer.Scene.defaultCamera.setPos(pos);
            renderer.Scene.defaultCamera.lookAt(lookat);
            #endregion Camera

            renderer.Scene.setAmbient(warp_Color.getColor(192, 191, 173));
            renderer.Scene.addLight("Light1", new warp_Light(new warp_Vector(0f, 1f, 8f), warp_Color.White, 0, 200, 20));

            CreateWater(renderer);
            CreateTerrain(renderer);
            if (m_drawPrimVolume)
                CreateAllPrims(renderer);

            renderer.Render();
            Bitmap bitmap = renderer.Scene.getImage();

            renderer.Scene.destroy();
            renderer.Reset();
            renderer = null;

            m_colors.Clear();
            m_warpTextures.Clear();

            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.Default;
            return bitmap;
        }

        public byte[] WriteJpeg2000Image()
        {
            try
            {
                using (Bitmap mapbmp = CreateMapTile())
                    return OpenJPEG.EncodeFromImage(mapbmp, false);
            }
            catch (Exception e)
            {
                // JPEG2000 encoder failed
                m_logger?.LogError("[WARP 3D IMAGE MODULE]: Failed generating terrain map: ", e);
            }

            return null;
        }

        #endregion

        #region Rendering Methods

        // Add a water plane to the renderer.
        private void CreateWater(WarpRenderer renderer)
        {
            float waterHeight = (float)m_scene.RegionInfo.RegionSettings.WaterHeight;

            renderer.AddPlane("Water", m_scene.RegionInfo.RegionSizeX * 0.5f, false);
            renderer.Scene.sceneobject("Water").setPos(m_scene.RegionInfo.RegionSizeX * 0.5f,
                                                       waterHeight,
                                                       m_scene.RegionInfo.RegionSizeY * 0.5f);

            warp_Material waterMaterial = new warp_Material(ConvertColor(WATER_COLOR));
            renderer.Scene.addMaterial("WaterMat", waterMaterial);
            renderer.SetObjectMaterial("Water", "WaterMat");
        }

        // Add a terrain to the renderer.
        // Note that we create a 'low resolution' 257x257 vertex terrain rather than trying for
        //    full resolution. This saves a lot of memory especially for very large regions.
        private void CreateTerrain(WarpRenderer renderer)
        {
            ITerrainChannel terrain = m_scene.Heightmap;

            float regionsx = m_scene.RegionInfo.RegionSizeX;
            float regionsy = m_scene.RegionInfo.RegionSizeY;

            // 'diff' is the difference in scale between the real region size and the size of terrain we're buiding

            int bitWidth;
            int bitHeight;

            bitWidth = Util.intLog2((uint)terrain.Width);
            bitHeight = Util.intLog2((uint)terrain.Height);

            if (bitWidth > 8) // more than 256 is very heavy :(
                bitWidth = 8;
            if (bitHeight > 8)
                bitHeight = 8;

            int twidth = (int)Math.Pow(2, bitWidth);
            int theight = (int)Math.Pow(2, bitHeight);

            float diff = regionsx / twidth;

            int npointsx = (int)(regionsx / diff);
            int npointsy = (int)(regionsy / diff);

            float invsx = 1.0f / (npointsx * diff);
            float invsy = 1.0f / (npointsy * diff);

            npointsx++;
            npointsy++;

            // Create all the vertices for the terrain
            warp_Object obj = new warp_Object();
            warp_Vector pos;
            float x, y;
            float tv;
            for (y = 0; y < regionsy; y += diff)
            {
                tv = y * invsy;
                for (x = 0; x < regionsx; x += diff)
                {
                    pos = ConvertVector(x, y, (float)terrain[(int)x, (int)y]);
                    obj.addVertex(new warp_Vertex(pos, x * invsx, tv));
                }
                pos = ConvertVector(x, y, (float)terrain[(int)(x - diff), (int)y]);
                obj.addVertex(new warp_Vertex(pos, 1.0f, tv));
            }

            int lastY = (int)(y - diff);
            for (x = 0; x < regionsx; x += diff)
            {
                pos = ConvertVector(x, y, (float)terrain[(int)x, lastY]);
                obj.addVertex(new warp_Vertex(pos, x * invsx, 1.0f));
            }
            pos = ConvertVector(x, y, (float)terrain[(int)(x - diff), lastY]);
            obj.addVertex(new warp_Vertex(pos, 1.0f, 1.0f));

            // create triangles.
            int limx = npointsx - 1;
            int limy = npointsy - 1;
            for (int j = 0; j < limy; j++)
            {
                for (int i = 0; i < limx; i++)
                {
                    int v = j * npointsx + i;

                    // Make two triangles for each of the squares in the grid of vertices
                    obj.addTriangle(
                        v,
                        v + 1,
                        v + npointsx);

                    obj.addTriangle(
                        v + npointsx + 1,
                        v + npointsx,
                        v + 1);
                }
            }

            renderer.Scene.addObject("Terrain", obj);

            UUID[] textureIDs = new UUID[4];
            float[] startHeights = new float[4];
            float[] heightRanges = new float[4];

            OpenSim.Framework.RegionSettings regionInfo = m_scene.RegionInfo.RegionSettings;

            textureIDs[0] = regionInfo.TerrainTexture1;
            textureIDs[1] = regionInfo.TerrainTexture2;
            textureIDs[2] = regionInfo.TerrainTexture3;
            textureIDs[3] = regionInfo.TerrainTexture4;

            startHeights[0] = (float)regionInfo.Elevation1SW;
            startHeights[1] = (float)regionInfo.Elevation1NW;
            startHeights[2] = (float)regionInfo.Elevation1SE;
            startHeights[3] = (float)regionInfo.Elevation1NE;

            heightRanges[0] = (float)regionInfo.Elevation2SW;
            heightRanges[1] = (float)regionInfo.Elevation2NW;
            heightRanges[2] = (float)regionInfo.Elevation2SE;
            heightRanges[3] = (float)regionInfo.Elevation2NE;

            warp_Texture texture;
            using (Bitmap image = TerrainSplat.Splat(terrain, textureIDs, startHeights, heightRanges,
                        m_scene.RegionInfo.WorldLocX, m_scene.RegionInfo.WorldLocY,
                        m_scene.AssetService, m_imgDecoder, m_textureTerrain, m_textureAverageTerrain,
                        twidth, twidth))
                    texture = new warp_Texture(image);

            warp_Material material = new warp_Material(texture);
            renderer.Scene.addMaterial("TerrainMat", material);
            renderer.SetObjectMaterial("Terrain", "TerrainMat");
        }

        private void CreateAllPrims(WarpRenderer renderer)
        {
            if (m_primMesher == null)
                return;

            m_scene.ForEachSOG(
                delegate (SceneObjectGroup group)
                {
                    foreach (SceneObjectPart child in group.Parts)
                        CreatePrim(renderer, child);
                }
            );
        }

        private void UVPlanarMap(Vertex v, Vector3 scale, out float tu, out float tv)
        {
            Vector3 scaledPos = v.Position * scale;
            float d = v.Normal.X;
            if (d >= 0.5f)
            {
                tu = 2f * scaledPos.Y;
                tv = scaledPos.X * v.Normal.Z - scaledPos.Z * v.Normal.X;
            }
            else if( d <= -0.5f)
            {
                tu = -2f * scaledPos.Y;
                tv = -scaledPos.X * v.Normal.Z + scaledPos.Z * v.Normal.X;
            }
            else if (v.Normal.Y > 0f)
            {
                tu = -2f * scaledPos.X;
                tv = scaledPos.Y * v.Normal.Z - scaledPos.Z * v.Normal.Y;
            }
            else 
            {
                tu = 2f * scaledPos.X;
                tv = -scaledPos.Y * v.Normal.Z + scaledPos.Z * v.Normal.Y;
            }

            tv *= 2f;
        }

        private void CreatePrim(WarpRenderer renderer, SceneObjectPart prim)
        {
            if ((PCode)prim.Shape.PCode != PCode.Prim)
                return;

            Vector3 ppos = prim.GetWorldPosition();
            if (ppos.Z < m_renderMinHeight || ppos.Z > m_renderMaxHeight)
                return;

            warp_Vector primPos = ConvertVector(ppos);
            warp_Quaternion primRot = ConvertQuaternion(prim.GetWorldRotation());
            warp_Matrix m = warp_Matrix.quaternionMatrix(primRot);

            float screenFactor = renderer.Scene.EstimateBoxProjectedArea(primPos, ConvertVector(prim.Scale), m);
            if (screenFactor < 0)
                return;

            const float log2inv = -1.442695f;
            int p2 = (int)((float)Math.Log(screenFactor) * log2inv * 0.25 - 1);

            if (p2 < 0)
                p2 = 0;
            else if (p2 > 3)
                p2 = 3;

            DetailLevel lod = (DetailLevel)(3 - p2);

            FacetedMesh renderMesh = null;
            Primitive omvPrim = prim.Shape.ToOmvPrimitive(prim.OffsetPosition, prim.RotationOffset);

            if (m_renderMeshes)
            {
                if (omvPrim.Sculpt != null && !omvPrim.Sculpt.SculptTexture.IsZero())
                {
                    // Try fetchinng the asset
                    AssetBase sculptAsset = m_scene.AssetService.Get(omvPrim.Sculpt.SculptTexture.ToString());
                    if (sculptAsset != null)
                    {
                        // Is it a mesh?
                        if (omvPrim.Sculpt.Type == SculptType.Mesh)
                        {
                            AssetMesh meshAsset = new AssetMesh(omvPrim.Sculpt.SculptTexture, sculptAsset.Data);
                            FacetedMesh.TryDecodeFromAsset(omvPrim, meshAsset, lod, out renderMesh);
                            meshAsset = null;
                        }
                        else // It's sculptie
                        {
                            if (m_imgDecoder != null)
                            {
                                Image sculpt = m_imgDecoder.DecodeToImage(sculptAsset.Data);
                                if (sculpt != null)
                                {
                                    renderMesh = m_primMesher.GenerateFacetedSculptMesh(omvPrim, (Bitmap)sculpt, lod);
                                    sculpt.Dispose();
                                }
                            }
                        }
                    }
                    else
                    {
                        m_logger?.LogWarning("[Warp3D] failed to get mesh or sculpt asset {0} of prim {1} at {2}",
                            omvPrim.Sculpt.SculptTexture.ToString(), prim.Name, prim.GetWorldPosition().ToString());
                    }
                }
            }

            // If not a mesh or sculptie, try the regular mesher
            if (renderMesh == null)
            {
                renderMesh = m_primMesher.GenerateFacetedMesh(omvPrim, lod);
            }

            if (renderMesh == null)
                return;

            string primID = prim.UUID.ToString();

            // Create the prim faces
            // TODO: Implement the useTextures flag behavior
            for (int i = 0; i < renderMesh.Faces.Count; i++)
            {
                Face face = renderMesh.Faces[i];
                string meshName = primID + i.ToString();

                // Avoid adding duplicate meshes to the scene
                if (renderer.Scene.objectData.ContainsKey(meshName))
                    continue;

                warp_Object faceObj = new warp_Object();

                Primitive.TextureEntryFace teFace = prim.Shape.Textures.GetFace((uint)i);
                Color4 faceColor = teFace.RGBA;
                if (faceColor.A == 0)
                    continue;

                string materialName = string.Empty;
                if (m_texturePrims)
                {
                    // if(lod > DetailLevel.Low)
                    {
                        // materialName = GetOrCreateMaterial(renderer, faceColor, teFace.TextureID, lod == DetailLevel.Low);
                        materialName = GetOrCreateMaterial(renderer, faceColor, teFace.TextureID, false, prim);
                        if (String.IsNullOrEmpty(materialName))
                            continue;
                        int c = renderer.Scene.material(materialName).getColor();
                        if ((c & warp_Color.MASKALPHA) == 0)
                            continue;
                    }
                }
                else
                    materialName = GetOrCreateMaterial(renderer, faceColor);

                if (renderer.Scene.material(materialName).getTexture() == null)
                {
                    // uv map details dont not matter for color;
                    for (int j = 0; j < face.Vertices.Count; j++)
                    {
                        Vertex v = face.Vertices[j];
                        warp_Vector pos = ConvertVector(v.Position);
                        warp_Vertex vert = new warp_Vertex(pos, v.TexCoord.X, v.TexCoord.Y);
                        faceObj.addVertex(vert);
                    }
                }
                else
                {
                    float tu;
                    float tv;
                    float offsetu = teFace.OffsetU + 0.5f;
                    float offsetv = teFace.OffsetV + 0.5f;
                    float scaleu = teFace.RepeatU;
                    float scalev = teFace.RepeatV;
                    float rotation = teFace.Rotation;
                    float rc = 0;
                    float rs = 0;
                    if (rotation != 0)
                    {
                        rc = (float)Math.Cos(rotation);
                        rs = (float)Math.Sin(rotation);
                    }

                    for (int j = 0; j < face.Vertices.Count; j++)
                    {
                        warp_Vertex vert;
                        Vertex v = face.Vertices[j];
                        warp_Vector pos = ConvertVector(v.Position);
                        if(teFace.TexMapType == MappingType.Planar)
                            UVPlanarMap(v, prim.Scale,out tu, out tv);
                        else
                        {
                            tu = v.TexCoord.X - 0.5f;
                            tv = 0.5f - v.TexCoord.Y;
                        }
                        if (rotation != 0)
                        {
                            float tur = tu * rc - tv * rs;
                            float tvr = tu * rs + tv * rc;
                            tur *= scaleu;
                            tur += offsetu;

                            tvr *= scalev;
                            tvr += offsetv;
                            vert = new warp_Vertex(pos, tur, tvr);
                        }
                        else
                        {
                            tu *= scaleu;
                            tu += offsetu;
                            tv *= scalev;
                            tv += offsetv;
                            vert = new warp_Vertex(pos, tu, tv);
                        }

                        faceObj.addVertex(vert);
                    }
                }

                for (int j = 0; j < face.Indices.Count; j += 3)
                {
                    faceObj.addTriangle(
                        face.Indices[j + 0],
                        face.Indices[j + 1],
                        face.Indices[j + 2]);
                }

                faceObj.scaleSelf(prim.Scale.X, prim.Scale.Z, prim.Scale.Y);
                faceObj.transform(m);
                faceObj.setPos(primPos);

                renderer.Scene.addObject(meshName, faceObj);
                renderer.SetObjectMaterial(meshName, materialName);
            }
        }

        private int GetFaceColor(Primitive.TextureEntryFace face)
        {
            int color;
            Color4 ctmp = Color4.White;

            if (face.TextureID.IsZero())
                return warp_Color.White;

            if (!m_colors.TryGetValue(face.TextureID, out color))
            {
                bool fetched = false;

                // Attempt to fetch the texture metadata
                string cacheName = "MAPCLR" + face.TextureID.ToString();
                AssetBase metadata = m_scene.AssetService.GetCached(cacheName);
                if (metadata != null)
                {
                    OSDMap map = null;
                    try { map = OSDParser.Deserialize(metadata.Data) as OSDMap; } catch { }

                    if (map != null)
                    {
                        ctmp = map["X-RGBA"].AsColor4();
                        fetched = true;
                    }
                }

                if (!fetched)
                {
                    // Fetch the texture, decode and get the average color,
                    // then save it to a temporary metadata asset
                    AssetBase textureAsset = m_scene.AssetService.Get(face.TextureID.ToString());
                    if (textureAsset != null)
                    {
                        int width, height;
                        ctmp = GetAverageColor(textureAsset.FullID, textureAsset.Data, out width, out height);

                        OSDMap data = new OSDMap { { "X-RGBA", OSD.FromColor4(ctmp) } };
                        metadata = new AssetBase
                        {
                            Data = System.Text.Encoding.UTF8.GetBytes(OSDParser.SerializeJsonString(data)),
                            Description = "Metadata for texture color" + face.TextureID.ToString(),
                            Flags = AssetFlags.Collectable,
                            FullID = UUID.Zero,
                            ID = cacheName,
                            Local = true,
                            Temporary = true,
                            Name = String.Empty,
                            Type = (sbyte)AssetType.Unknown
                        };
                        m_scene.AssetService.Store(metadata);
                    }
                    else
                    {
                        ctmp = new Color4(0.5f, 0.5f, 0.5f, 1.0f);
                    }
                }
                color = ConvertColor(ctmp);
                m_colors[face.TextureID] = color;
            }

            return color;
        }

        private string GetOrCreateMaterial(WarpRenderer renderer, Color4 color)
        {
            string name = color.ToString();

            warp_Material material = renderer.Scene.material(name);
            if (material != null)
                return name;

            renderer.AddMaterial(name, ConvertColor(color));
            return name;
        }

        public string GetOrCreateMaterial(WarpRenderer renderer, Color4 faceColor, UUID textureID, bool useAverageTextureColor, SceneObjectPart sop)
        {
            int color = ConvertColor(faceColor);
            string idstr = textureID.ToString() + color.ToString();
            string materialName = "MAPMAT" + idstr;

            if (renderer.Scene.material(materialName) != null)
                return materialName;

            warp_Material mat = new warp_Material();
            warp_Texture texture = GetTexture(textureID, sop);
            if (texture != null)
            {
                if (useAverageTextureColor)
                    color = warp_Color.multiply(color, texture.averageColor);
                else
                    mat.setTexture(texture);
            }
            else
                color = warp_Color.multiply(color, warp_Color.Grey);

            mat.setColor(color);
            renderer.Scene.addMaterial(materialName, mat);

            return materialName;
        }

        private warp_Texture GetTexture(UUID id, SceneObjectPart sop)
        {
            warp_Texture ret = null;
            if (id.IsZero())
                return ret;
            if (m_warpTextures.TryGetValue(id, out ret))
                return ret;

            AssetBase asset = m_scene.AssetService.Get(id.ToString());
            if (asset != null)
            {
                try
                {
                    using (Bitmap img = (Bitmap)m_imgDecoder.DecodeToImage(asset.Data))
                        ret = new warp_Texture(img, 8); // reduce textures size to 256x256
                }
                catch (Exception e)
                {
                    m_logger?.LogWarning("[Warp3D]: Failed to decode texture {0} for prim {1} at {2}, exception {3}", id.ToString(), sop.Name, sop.GetWorldPosition().ToString(), e.Message);
                }
            }
            else
                m_logger?.LogWarning("[Warp3D]: missing texture {0} data for prim {1} at {2}",
                    id.ToString(), sop.Name, sop.GetWorldPosition().ToString());

            m_warpTextures[id] = ret;
            return ret;
        }

        #endregion Rendering Methods

        #region Static Helpers
        // Note: axis change.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static warp_Vector ConvertVector(float x, float y, float z)
        {
            return new warp_Vector(x, z, y);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static warp_Vector ConvertVector(Vector3 vector)
        {
            return new warp_Vector(vector.X, vector.Z, vector.Y);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static warp_Quaternion ConvertQuaternion(Quaternion quat)
        {
            return new warp_Quaternion(quat.X, quat.Z, quat.Y, -quat.W);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private static int ConvertColor(Color4 color)
        {
            int c = warp_Color.getColor((byte)(color.R * 255f), (byte)(color.G * 255f), (byte)(color.B * 255f), (byte)(color.A * 255f));
            return c;
        }

        private static Vector3 SurfaceNormal(Vector3 c1, Vector3 c2, Vector3 c3)
        {
            Vector3 normal = Vector3.Cross(c2 - c1, c3 - c1);
            normal.Normalize();

            return normal;
        }

        public Color4 GetAverageColor(UUID textureID, byte[] j2kData, out int width, out int height)
        {
            ulong r = 0;
            ulong g = 0;
            ulong b = 0;
            ulong a = 0;
            int pixelBytes;

            try
            {
                using (MemoryStream stream = new MemoryStream(j2kData))
                using (Bitmap bitmap = (Bitmap)J2kImage.FromStream(stream))
                {
                    width = bitmap.Width;
                    height = bitmap.Height;

                    BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, bitmap.PixelFormat);
                    pixelBytes = (bitmapData.PixelFormat == PixelFormat.Format24bppRgb) ? 3 : 4;

                    // Sum up the individual channels
                    unsafe
                    {
                        byte* start = (byte*)bitmapData.Scan0;
                        if (pixelBytes == 4)
                        {
                            for (int y = 0; y < height; y++)
                            {
                                
                                byte* end = start + 4 * width;
                                for(byte* row = start; row < end; row += 4)
                                {
                                    b += row[0];
                                    g += row[1];
                                    r += row[2];
                                    a += row[3];
                                }
                                start += bitmapData.Stride;
                            }
                        }
                        else
                        {
                            for (int y = 0; y < height; y++)
                            {
                                byte* end = start + 3 * width;
                                for (byte* row = start; row < end; row += 3)
                                {
                                    b += row[0];
                                    g += row[1];
                                    r += row[2];
                                }
                                start += bitmapData.Stride;
                            }
                        }
                    }
                    bitmap.UnlockBits(bitmapData);
                }
                // Get the averages for each channel
                double invtotalPixels = 1.0/(255.0 * width * height);
                double rm = r * invtotalPixels;
                double gm = g * invtotalPixels;
                double bm = b * invtotalPixels;
                double am = pixelBytes == 3 ? 1.0 : a * invtotalPixels;
                return new Color4((float)rm, (float)gm, (float)bm, (float)am);
            }
            catch (Exception ex)
            {
                m_logger?.LogWarning(
                    "[WARP 3D IMAGE MODULE]: Error decoding JPEG2000 texture {0} ({1} bytes): {2}",
                    textureID, j2kData.Length, ex.Message);

                width = 0;
                height = 0;
                return new Color4(0.5f, 0.5f, 0.5f, 1.0f);
            }
        }

        #endregion Static Helpers
    }

    public static class ImageUtils
    {
        /// <summary>
        /// Performs bilinear interpolation between four values
        /// </summary>
        /// <param name="v00">First, or top left value</param>
        /// <param name="v01">Second, or top right value</param>
        /// <param name="v10">Third, or bottom left value</param>
        /// <param name="v11">Fourth, or bottom right value</param>
        /// <param name="xPercent">Interpolation value on the X axis, between 0.0 and 1.0</param>
        /// <param name="yPercent">Interpolation value on fht Y axis, between 0.0 and 1.0</param>
        /// <returns>The bilinearly interpolated result</returns>
        public static float Bilinear(float v00, float v01, float v10, float v11, float xPercent, float yPercent)
        {
            return Utils.Lerp(Utils.Lerp(v00, v01, xPercent), Utils.Lerp(v10, v11, xPercent), yPercent);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static float Bilinear(float[] v, float xPercent, float yPercent)
        {
            return Utils.Lerp(Utils.Lerp(v[0], v[2], xPercent), Utils.Lerp(v[1], v[3], xPercent), yPercent);
        }
    }
}