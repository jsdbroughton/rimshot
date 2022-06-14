﻿using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.DocumentParts;
using CefSharp;
using Speckle.Core.Api;
using Speckle.Core.Credentials;
using Speckle.Core.Logging;
using Speckle.Core.Models;
using Speckle.Core.Transports;
using Speckle.Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using Color = System.Drawing.Color;
using ComApi = Autodesk.Navisworks.Api.Interop.ComApi;
using ComBridge = Autodesk.Navisworks.Api.ComApi.ComApiBridge; //
using Navis = Autodesk.Navisworks.Api.Application;
using NavisworksApp = Autodesk.Navisworks.Api.Application;
using Objects.Geometry;
using Rimshot.Shared.ArrayExtensions;
using System.Collections.Concurrent;
using System.Threading;

namespace Rimshot.Shared.Workshop {
  public abstract class UIBindings {
#if DEBUG
    //const string rimshotUrl = "http://192.168.86.29:8080/issues";
    const string rimshotUrl = "https://rimshot.app/issues";
#else
    const String rimshotUrl = "https://rimshot.app/issues";
#endif
    public const string Url = rimshotUrl;
    private const int V = 131004;
    private const int Modulo = 5;
    private string _tempFolder = "";

    public IWebBrowser Browser { get; set; }

    public IssueListPane Window { get; set; }
    public DocumentModels Models { get => this.Models; set => this.Models = value; }

    private string image;

    // base64 encoding of the image
    public virtual string GetImage () => this.image;

    // base64 encoding of the image
    public virtual void SetImage ( string value ) => this.image = value;

    public virtual void NotifyUI ( string eventName, dynamic eventInfo ) {
      string script = string.Format( "window.EventBus.$emit('{0}',{1})", eventName, JsonConvert.SerializeObject( eventInfo ) );
      Browser.GetMainFrame().EvaluateScriptAsync( script );
    }

    public virtual void CommitStoreMutationUI ( string storeMutationName, string args = null ) {
      string script = string.Format( "window.Store.commit('{0}', '{1}')", storeMutationName, args );
      try {
        Browser.GetMainFrame().EvaluateScriptAsync( script );
      } catch ( Exception e ) {
        Console.WriteLine( e.Message );
      }
    }

    public virtual void DispatchStoreActionUI ( string storeActionName, string args = null ) {
      string script = string.Format( "window.Store.dispatch('{0}', '{1}')", storeActionName, args );
      try {
        Browser.GetMainFrame().EvaluateScriptAsync( script );
      } catch ( Exception e ) {
        Console.WriteLine( e.Message );
      }
    }

    public virtual void ShowDev () => this.Browser.ShowDevTools();
    public virtual void Refresh ( bool force = false ) {
      this.Browser.Reload( force );
      this.Browser.GetMainFrame().LoadUrl( Url );
    }

    public virtual void AddImage () => this.SendIssueView();

    public virtual void CommitSelection ( object payload ) => CommitSelectedObjectsToSpeckle( payload );

    private void RimshotError ( Exception err ) {
      Console.ForegroundColor = ConsoleColor.Red;
      Console.WriteLine( err.Message.ToString() );
      Console.ForegroundColor = ConsoleColor.Gray;

      if ( err is SpeckleException ) {
        NotifyUI( "error", new { message = err.Message } );
        return;
      }
      throw err;
    }

    public virtual string UpdateView ( string camera ) {
      Console.WriteLine( camera );
      return camera;
    }

    public Document ActiveDocument { get; set; }
    private Vector3D TransformVector3D { get; set; }

    private Objects.Geometry.Vector SettingOutPoint { get; set; }
    private Objects.Geometry.Vector TransformVector { get; set; }

    private async void CommitSelectedObjectsToSpeckle ( object payload ) {

      RimshotConsole( "Commit Selection commenced." );

      dynamic commitPayload = ( dynamic )payload;

      string branchName = commitPayload.branch;
      string streamId = commitPayload.stream;
      string host = commitPayload.host;
      string issueId = commitPayload.issueId;

      GeometrySet.Clear();

      RimshotConsole( $"Stream: {streamId}, Host: {host}, Branch: {branchName}, Issue: {issueId}" );

      string description = $"issueId:{issueId}";

      ActiveDocument = NavisworksApp.ActiveDocument;
      DocumentModels documentModels = ActiveDocument.Models;

      ModelItemCollection appSelectedItems = NavisworksApp.ActiveDocument.CurrentSelection.SelectedItems;

      // Were the selection to change mid-commit, accessing the selected set would fail.
      ModelItemCollection selectedItems = new ModelItemCollection();
      appSelectedItems.CopyTo( selectedItems );

      if ( selectedItems.IsEmpty ) {
        RimshotConsole( "Nothin Selected." );
        NotifyUI( "error", JsonConvert.SerializeObject( new { message = "Nothing Selected." } ) );
        NotifyUI( "commit_sent", new { commit = "", issueId, stream = streamId, objectId = "" } );
        return;
      }

      ModelItem firstItem = selectedItems.First();
      ModelItem root = firstItem.Parent ?? firstItem;
      ModelGeometry temp = root.FindFirstGeometry();

      BoundingBox3D modelBoundingBox = temp.BoundingBox;
      Point3D center = modelBoundingBox.Center;

      TransformVector3D = new Vector3D( -center.X, -center.Y, 0 );

      SettingOutPoint = new Objects.Geometry.Vector {
        x = modelBoundingBox.Min.X,
        y = modelBoundingBox.Min.Y,
        z = 0
      };

      TransformVector = new Objects.Geometry.Vector {
        x = -TransformVector3D.X,
        y = -TransformVector3D.Y,
        z = 0
      };

      Account defaultAccount = AccountManager.GetDefaultAccount();

      if ( defaultAccount == null ) {
        RimshotError( new SpeckleException( $"You do not have any account. Please create one or add it to the Speckle Manager." ) );
        return;
      }

      RimshotConsole( defaultAccount.serverInfo.url );
      RimshotConsole( defaultAccount.userInfo.email );

      Client client = new Client( defaultAccount );

      try {
        await client.StreamGet( streamId );
      } catch {
        RimshotError(
        new SpeckleException(
          $"You don't have access to stream {streamId} on server {host}, or the stream does not exist." ) );
        return;
      }

      // Get Branch and create if it doesn't exist.
      Branch branch;
      try {
        branch = await client.BranchGet( streamId, branchName );
        if ( branch is null ) {
          branch = await CreateBranch( client, streamId, branchName, description );
        }
      } catch ( Exception ) {
        RimshotError( new SpeckleException( $"Unable to find or create an issue branch for {branchName}" ) );
        return;
      }

      try {
        branch = client.BranchGet( streamId, branchName, 1 ).Result;

        if ( branch != null ) {
          NotifyUI( "branch_updated", JsonConvert.SerializeObject( new { branch = branch.name, issueId } ) );
        }
      } catch ( Exception ) {
        RimshotError( new SpeckleException( $"Unable to find an issue branch for {branchName}" ) );
        return;
      }

      List<Base> translatedElements = new List<Base>();

      ConcurrentStack<Base> Elements = new ConcurrentStack<Base>();

      int elementCount = selectedItems.Count;

      // Iterate the selected elements regardless of their position in the tree
      HashSet<NavisGeometry> geometrySet = new HashSet<NavisGeometry>();

      ConcurrentDictionary<NavisGeometry, Boolean> geometryDict = new ConcurrentDictionary<NavisGeometry, Boolean>();

      for ( int e = 0; e < elementCount; e++ ) {
        ModelItem element = selectedItems[ e ];
        List<ModelItem> geometryNodes = CollectGeometryNodes( element );

        foreach ( ModelItem n in geometryNodes ) {
          NavisGeometry g = new NavisGeometry( n );
          geometryDict.TryAdd( g, true );
        }
      }

      ConcurrentStack<bool> doneElements = new ConcurrentStack<bool>();

      List<Task> fragmentTasks = new List<Task>();
      foreach ( KeyValuePair<NavisGeometry, bool> entry in geometryDict ) {
        fragmentTasks.Add( Task.Run( () => {
          AddFragments( entry.Key );
          doneElements.Push( true );
          RimshotConsole( $"{doneElements.Count} of {geometryDict.Count}", ConsoleColor.DarkGreen );
        } ) );
      }
      Task t = Task.WhenAll( fragmentTasks );

      t.Wait();

      geometrySet.UnionWith( geometryDict.Keys );

      List<Task> elementTasks = new List<Task>();
      doneElements.Clear();
      foreach ( NavisGeometry navisGeometry in geometrySet ) {
        elementTasks.Add( Task.Run( () => {
          Elements.Push( TranslateGeometryElement( navisGeometry ) );
          doneElements.Push( true );
          NotifyUI( "element-progress", JsonConvert.SerializeObject( new { current = doneElements.Count, count = geometrySet.Count } ) );
          RimshotConsole( $"Element {doneElements.Count} of {geometrySet.Count}" );
        } ) );
      }

      t = Task.WhenAll( elementTasks );
      t.Wait();

      RimshotConsole( "Build Commit Object" );
      Base myCommit = new Base();
      myCommit[ "@Elements" ] = Elements.ToList();

      myCommit[ "TransformVector" ] = TransformVector;
      myCommit[ "Real World Coordinates" ] = SettingOutPoint;

      myCommit[ "bbox" ] = BoxToSpeckle( selectedItems.BoundingBox() );

      string[] stringseparators = new string[] { "/" };
      myCommit[ "Issue Number" ] = branchName.Split( stringseparators, StringSplitOptions.None )[ 1 ];
      myCommit[ "applicationId" ] = issueId;
      ServerTransport transport = new ServerTransport( defaultAccount, streamId );
      string hash = Operations.Send( myCommit, new List<ITransport> { transport } ).Result;

      RimshotConsole( "Commit Create" );
      string commitId = client.CommitCreate( new CommitCreateInput() {
        branchName = branchName,
        message = "Rimshot issue commit.",
        objectId = hash,
        streamId = streamId,
        sourceApplication = "Rimshot"
      } ).Result;

      Commit commitObject = client.CommitGet( streamId, commitId ).Result;
      string referencedObject = commitObject.referencedObject;

      RimshotConsole( "Commit Sent" );
      NotifyUI( "commit_sent", new { commit = commitId, issueId, stream = streamId, objectId = referencedObject } );

      client.Dispose();
    }

    private static void RimshotConsole ( string message, ConsoleColor color = ConsoleColor.Blue ) {
      Console.ForegroundColor = color;
      Console.WriteLine( message );
      Console.ForegroundColor = ConsoleColor.Gray;
    }

    public Dictionary<int[], Stack<ComApi.InwOaFragment3>> pathDictionary = new Dictionary<int[], Stack<ComApi.InwOaFragment3>>();
    public Dictionary<NavisGeometry, Stack<ComApi.InwOaFragment3>> modelGeometryDictionary = new Dictionary<NavisGeometry, Stack<ComApi.InwOaFragment3>>();
    public HashSet<NavisGeometry> GeometrySet = new HashSet<NavisGeometry>();

    public void AddFragments ( NavisGeometry geometry ) {

      geometry.ModelFragments = new Stack<ComApi.InwOaFragment3>();

      foreach ( ComApi.InwOaPath path in geometry.ComSelection.Paths() ) {
        foreach ( ComApi.InwOaFragment3 frag in path.Fragments() ) {

          int[] a1 = ( ( Array )frag.path.ArrayData ).ToArray<int>();
          int[] a2 = ( ( Array )path.ArrayData ).ToArray<int>();
          bool isSame = true;

          if ( a1.Length != a2.Length || !Enumerable.SequenceEqual( a1, a2 ) ) {
            isSame = false;
          }

          if ( isSame ) {
            geometry.ModelFragments.Push( frag );
          }
        }
      }
    }

    public void GetSortedFragments ( ModelItemCollection modelItems ) {
      ComApi.InwOpSelection oSel = ComBridge.ToInwOpSelection( modelItems );
      // To be most efficient you need to lookup an efficient EqualityComparer
      // for the int[] key
      foreach ( ComApi.InwOaPath3 path in oSel.Paths() ) {
        // this yields ONLY unique fragments
        // ordered by geometry they belong to
        foreach ( ComApi.InwOaFragment3 frag in path.Fragments() ) {
          int[] pathArr = ( ( Array )frag.path.ArrayData ).ToArray<int>();
          if ( !this.pathDictionary.TryGetValue( pathArr, out Stack<ComApi.InwOaFragment3> frags ) ) {
            frags = new Stack<ComApi.InwOaFragment3>();
            this.pathDictionary[ pathArr ] = frags;
          }
          frags.Push( frag );
        }
      }
    }

    /// <summary>
    /// Parse all descendant nodes of the element that are geometry nodes.
    /// </summary>
    public List<ModelItem> CollectGeometryNodes ( ModelItem element ) {
      ModelItemEnumerableCollection descendants = element.DescendantsAndSelf;
      ModelItemCollection geometryElements = new ModelItemCollection();

      return ( from item in descendants where item.HasGeometry && !item.IsHidden select item ).ToList();
    }
    public Base TranslateGeometryElement ( NavisGeometry geometryElement ) {
      Base elementBase = new Base();

      if ( geometryElement.ModelItem.HasGeometry && geometryElement.ModelItem.Children.Count() == 0 ) {
        List<Base> speckleGeometries = TranslateFragmentGeometry( geometryElement );
        if ( speckleGeometries.Count > 0 ) {
          elementBase[ "displayValue" ] = speckleGeometries;
          elementBase[ "units" ] = "m";
          elementBase[ "bbox" ] = BoxToSpeckle( geometryElement.ModelItem.BoundingBox() );
        }
      }
      return elementBase;
    }

    private Box BoxToSpeckle ( BoundingBox3D boundingBox3D ) {
      Box boundingBox = new Box();

      double scale = 0.001; // TODO: Proper units support.

      Point3D min = boundingBox3D.Min;
      Point3D max = boundingBox3D.Max;

      boundingBox.xSize = new Objects.Primitive.Interval( min.X * scale, max.X * scale );
      boundingBox.ySize = new Objects.Primitive.Interval( min.Y * scale, max.Y * scale );
      boundingBox.zSize = new Objects.Primitive.Interval( min.Z * scale, max.Z * scale );

      return boundingBox;
    }

    public Base TranslateElement ( ModelItem element ) {
      Base elementBase = new Base();

      //int descendantsCount = element.Descendants.Count();
      int descendantsCount = element.Children.Count();

      if ( element.HasGeometry && descendantsCount == 0 ) {
        List<Objects.Geometry.Mesh> geometryToSpeckle = TranslateGeometry( element );
        if ( geometryToSpeckle.Count > 0 ) {
          elementBase[ "displayValue" ] = geometryToSpeckle;

          // TODO: this needs to reflect model reality it will be a property of the ActiveDocument no doubt.
          // Or perhaps always target metres and convert appropriately
          elementBase[ "units" ] = "m";
        }
      }

      List<Base> children = new List<Base>();

      if ( descendantsCount > 0 ) {
        for ( int d = 0; d < descendantsCount; d++ ) {
          ModelItem child = element.Children.ElementAt( d );
          // TODO: work out a performant way to keep a progress UI element up to date.
          children.Add( TranslateElement( child ) );
        }
        elementBase[ "@Elements" ] = children;
      }

      Base propertiesBase = new Base();

      PropertyCategoryCollection propertyCategories = element.PropertyCategories;

      foreach ( PropertyCategory propCategory in propertyCategories ) {

        DataPropertyCollection properties = propCategory.Properties;
        Base propertyCategoryBase = new Base();
        string propertyCategoryName = SanitizePropertyName( propCategory.DisplayName );

        foreach ( DataProperty property in properties ) {
          string key;
          try {

            key = SanitizePropertyName( property.DisplayName.ToString() );
          } catch ( Exception err ) {
            Console.WriteLine( $"Property Name not converted. {err.Message}" );
            break;
          }

          dynamic propValue = null;

          switch ( property.Value.DataType ) {
            case VariantDataType.Boolean: propValue = property.Value.ToBoolean().ToString(); break;
            case VariantDataType.DisplayString: propValue = property.Value.ToDisplayString(); break;
            case VariantDataType.IdentifierString: propValue = property.Value.ToIdentifierString(); break;
            case VariantDataType.Int32: propValue = property.Value.ToInt32().ToString(); break;
            case VariantDataType.Double: propValue = property.Value.ToDouble().ToString(); break;
            case VariantDataType.DoubleAngle: propValue = property.Value.ToDoubleAngle().ToString(); break;
            case VariantDataType.DoubleArea: propValue = property.Value.ToDoubleArea().ToString(); break;
            case VariantDataType.DoubleLength: propValue = property.Value.ToDoubleLength().ToString(); break;
            case VariantDataType.DoubleVolume: propValue = property.Value.ToDoubleVolume().ToString(); break;
            case VariantDataType.DateTime: propValue = property.Value.ToDateTime().ToString(); break;
            case VariantDataType.NamedConstant: propValue = property.Value.ToNamedConstant().ToString(); break;
            case VariantDataType.Point3D: propValue = property.Value.ToPoint3D().ToString(); break;
            case VariantDataType.None: break;
          }

          if ( propValue != null ) {

            object keyPropValue = propertyCategoryBase[ key ];

            if ( keyPropValue == null ) {
              propertyCategoryBase[ key ] = propValue;
            } else if ( keyPropValue.GetType().IsArray ) {
              List<dynamic> arrayPropValue = ( List<dynamic> )keyPropValue;
              arrayPropValue.Add( propValue );
              propertyCategoryBase[ key ] = arrayPropValue;
            } else {
              dynamic existingValue = keyPropValue;
              List<dynamic> arrayPropValue = new List<dynamic> {
                existingValue,
                propValue
              };
              propertyCategoryBase[ key ] = arrayPropValue;
            }

          }
        }

        if ( propertyCategoryBase.GetDynamicMembers().Count() > 0 && propertyCategoryName != null ) {
          if ( propertiesBase != null ) {

            if ( propertyCategoryName == "Geometry" ) {
              continue;
            }

            if ( propertyCategoryName == "Item" ) {
              propertiesBase[ "NavisItem" ] = propertyCategoryBase;
            } else {
              propertiesBase[ propertyCategoryName ] = propertyCategoryBase;
            }
          }
        }
      }

      elementBase[ "Properties" ] = propertiesBase;

      return elementBase;
    }

    public string SanitizePropertyName ( string name ) {
      // Regex pattern from speckle-sharp/Core/Core/Models/DynamicBase.cs IsPropNameValid
      string cleanName = Regex.Replace( name, @"[\.\/]", "_" );
      return cleanName;
    }

    public List<Base> TranslateFragmentGeometry ( NavisGeometry navisGeometry ) {
      List<Shared.CallbackGeomListener> callbackListeners = navisGeometry.GetUniqueFragments();

      List<Base> baseGeometries = new List<Base>();

      foreach ( Shared.CallbackGeomListener callback in callbackListeners ) {
        List<NavisDoubleTriangle> Triangles = callback.Triangles;
        // TODO: Additional Geometry Types
        //List<NavisDoubleLine> Lines = callback.Lines;
        //List<NavisDoublePoint> Points = callback.Points;

        List<double> vertices = new List<double>();
        List<int> faces = new List<int>();

        Vector3D move = this.TransformVector3D;

        int triangleCount = Triangles.Count;
        if ( triangleCount > 0 ) {

          for ( int t = 0; t < triangleCount; t += 1 ) {
            double scale = ( double )0.001; // TODO: This will need to relate to the ActiveDocument reality and the target units. Probably metres.

            // Apply the bounding box move.
            // The native API methods for overriding transforms are not thread safe to call from the CEF instance
            vertices.AddRange( new List<double>() {
              ( Triangles[ t ].Vertex1.X + move.X ) * scale,
              ( Triangles[ t ].Vertex1.Y + move.Y ) * scale,
              ( Triangles[ t ].Vertex1.Z + move.Z ) * scale
            } );
            vertices.AddRange( new List<double>() {
              ( Triangles[ t ].Vertex2.X + move.X ) * scale,
              ( Triangles[ t ].Vertex2.Y + move.Y ) * scale,
              ( Triangles[ t ].Vertex2.Z + move.Z ) * scale
            } );
            vertices.AddRange( new List<double>() {
              ( Triangles[ t ].Vertex3.X + move.X ) * scale,
              ( Triangles[ t ].Vertex3.Y + move.Y ) * scale,
              ( Triangles[ t ].Vertex3.Z + move.Z ) * scale
            } );

            // TODO: Move this back to Geometry.cs
            faces.Add( 0 );
            faces.Add( t * 3 );
            faces.Add( t * 3 + 1 );
            faces.Add( t * 3 + 2 );
          }
          Objects.Geometry.Mesh baseMesh = new Objects.Geometry.Mesh( vertices, faces );
          baseMesh[ "renderMaterial" ] = TranslateMaterial( navisGeometry.ModelItem );
          baseGeometries.Add( baseMesh );
        }
      }
      return baseGeometries; // TODO: Check if this actually has geometries before adding to DisplayValue
    }

    public List<Objects.Geometry.Mesh> TranslateGeometry ( ModelItem geom ) { // TODO: This should move to Geometry.cs or Conversions.cs

      NavisGeometry navisGeometry = new NavisGeometry( geom );
      List<Shared.CallbackGeomListener> cb = navisGeometry.GetFragments();

      List<Objects.Geometry.Mesh> baseMeshes = new List<Objects.Geometry.Mesh>();

      foreach ( Shared.CallbackGeomListener callback in cb ) {

        List<double> vertices = new List<double>();
        List<int> faces = new List<int>();
        List<NavisDoubleTriangle> Triangles = callback.Triangles;
        int triangleCount = Triangles.Count;

        if ( triangleCount > 0 ) {
          int c = 0;
          for ( int t = 0; t < triangleCount; t += 1 ) {

            // TODO: work out a performant way to keep a progress UI element up to date.
            double progress = ( ( double )t + 1 ) / ( double )triangleCount * 100;
            int c2 = ( int )Math.Truncate( progress );

            if ( Math.Truncate( progress ) % Modulo == 0 && c != c2 ) {
              c = c2;
              CommitStoreMutationUI( "SET_GEOMETRY_PROGRESS", JsonConvert.SerializeObject( new { current = t + 1, count = triangleCount } ) );
            }
            double scale = ( double )0.001; // TODO: This will need to relate to the ActiveDocument reality and the target units. Probably metres.

            vertices.AddRange( new List<double>() { Triangles[ t ].Vertex1.X * scale, Triangles[ t ].Vertex1.Y * scale, Triangles[ t ].Vertex1.Z * scale } );
            vertices.AddRange( new List<double>() { Triangles[ t ].Vertex2.X * scale, Triangles[ t ].Vertex2.Y * scale, Triangles[ t ].Vertex2.Z * scale } );
            vertices.AddRange( new List<double>() { Triangles[ t ].Vertex3.X * scale, Triangles[ t ].Vertex3.Y * scale, Triangles[ t ].Vertex3.Z * scale } );

            // TODO: Move this back to Geometry.cs
            faces.Add( 0 );
            faces.Add( t * 3 );
            faces.Add( t * 3 + 1 );
            faces.Add( t * 3 + 2 );
          }

          Objects.Geometry.Mesh baseMesh = new Objects.Geometry.Mesh( vertices, faces );
          baseMesh[ "renderMaterial" ] = TranslateMaterial( geom );
          baseMeshes.Add( baseMesh );
        }
      }

      return baseMeshes;
    }

    public Objects.Other.RenderMaterial TranslateMaterial ( ModelItem geom ) { // TODO: Extract this to a Conversions.cs or similar

      Color original = Color.FromArgb(
        Convert.ToInt32( geom.Geometry.OriginalColor.R * 255 ),
        Convert.ToInt32( geom.Geometry.OriginalColor.G * 255 ),
        Convert.ToInt32( geom.Geometry.OriginalColor.B * 255 ) );

      Color dark = Color.FromArgb( Convert.ToInt32( 0 ), Convert.ToInt32( 0 ), Convert.ToInt32( 0 ) );

      Objects.Other.RenderMaterial r = new Objects.Other.RenderMaterial( 1 - geom.Geometry.OriginalTransparency, 0, 1, original, dark ) {
        name = "NavisMaterial"
      };

      return r;
    }

    public Task<Branch> CreateBranch ( Client client, string streamId, string branchName, string description ) {
      try {
        Task<string> branchId = client.BranchCreate( new BranchCreateInput() {
          streamId = streamId,
          name = branchName,
          description = description
        } );
      } catch ( Exception e ) {
        NotifyUI( "error", new { message = e.Message } );
      }

      Task<Branch> branch = client.BranchGet( streamId, branchName );

      return branch;
    }
    private void SendIssueView () {

      MemoryStream stream;
      byte[] imageBytes;
      Guid id = Guid.NewGuid();
      SavedViewpoint oNewViewPt1 = new SavedViewpoint( Navis.ActiveDocument.CurrentViewpoint.ToViewpoint() ) {
        Guid = id,
        DisplayName = string.Format( "View - {0}", id )
      };
      Navis.ActiveDocument.SavedViewpoints.AddCopy( oNewViewPt1 );
      Image image = new Image( oNewViewPt1 ) { name = "View" };

      ComApi.InwOpState10 oState = ComBridge.State;
      ComApi.InwOaPropertyVec options = oState.GetIOPluginOptions( "lcodpimage" );

      _tempFolder = Path.Combine( Path.GetTempPath(), "Rimshot.ExportBCF", Path.GetRandomFileName() );

      string imageFolder = Path.Combine( this._tempFolder, image.guid.ToString() );
      string snapshot = Path.Combine( imageFolder, image.name + ".png" );

      if ( !Directory.Exists( this._tempFolder ) ) {
        Directory.CreateDirectory( this._tempFolder );
      }

      _ = Directory.CreateDirectory( imageFolder );

      foreach ( ComApi.InwOaProperty opt in options.Properties() ) {
        if ( opt.name == "export.image.format" ) {
          opt.value = "lcodpexpng";
        }

        if ( opt.name == "export.image.width" ) {
          opt.value = 1600;
        }

        if ( opt.name == "export.image.height" ) {
          opt.value = 900;
        }
      }
      oState.DriveIOPlugin( "lcodpimage", snapshot, options );

      try {
        stream = new MemoryStream();

        Bitmap oBitmap = new Bitmap( snapshot );
        Bitmap tBitmap = new Bitmap( snapshot );

        oBitmap.Save( stream, System.Drawing.Imaging.ImageFormat.Jpeg );
        imageBytes = stream.ToArray();
        image.image = Convert.ToBase64String( imageBytes );

        ImageViewpoint viewpoint = new ImageViewpoint( oNewViewPt1 );

      } catch ( Exception err ) {
        _ = MessageBox.Show( err.Message );
      }
      string imageString = JsonConvert.SerializeObject( image );

      SetImage( imageString );
      NotifyUI( "new-image", imageString );
    }
  }

  public class CallbackGeomListener : ComApi.InwSimplePrimitivesCB { // TODO: Move this to Geometry.cs
    public List<decimal> Coords { get; set; }
    public decimal[] Matrix { get; set; }
    public CallbackGeomListener () {
      Coords = new List<decimal>();
    }
    public void Line ( ComApi.InwSimpleVertex v1, ComApi.InwSimpleVertex v2 ) { }
    public void Point ( ComApi.InwSimpleVertex v1 ) { }
    public void SnapPoint ( ComApi.InwSimpleVertex v1 ) { }

    public void Triangle ( ComApi.InwSimpleVertex v1, ComApi.InwSimpleVertex v2, ComApi.InwSimpleVertex v3 ) {

      Array array_v1 = ( Array )v1.coord;
      decimal v1_x = ( decimal )array_v1.GetValue( 1 );
      decimal v1_y = ( decimal )array_v1.GetValue( 2 );
      decimal v1_z = ( decimal )array_v1.GetValue( 3 );

      Array array_v2 = ( Array )v2.coord;
      decimal v2_x = ( decimal )array_v2.GetValue( 1 );
      decimal v2_y = ( decimal )array_v2.GetValue( 2 );
      decimal v2_z = ( decimal )array_v2.GetValue( 3 );

      Array array_v3 = ( Array )v3.coord;
      decimal v3_x = ( decimal )array_v3.GetValue( 1 );
      decimal v3_y = ( decimal )array_v3.GetValue( 2 );
      decimal v3_z = ( decimal )array_v3.GetValue( 3 );

      //Matrix
      decimal w1 = this.Matrix[ 3 ] * v1_x + this.Matrix[ 7 ] * v1_y + this.Matrix[ 11 ] * v1_z + this.Matrix[ 15 ];
      decimal v1__x = ( this.Matrix[ 0 ] * v1_x + this.Matrix[ 4 ] * v1_y + this.Matrix[ 8 ] * v1_z + this.Matrix[ 12 ] ) / w1;
      decimal v1__y = ( this.Matrix[ 1 ] * v1_x + this.Matrix[ 5 ] * v1_y + this.Matrix[ 9 ] * v1_z + this.Matrix[ 13 ] ) / w1;
      decimal v1__z = ( this.Matrix[ 2 ] * v1_x + this.Matrix[ 6 ] * v1_y + this.Matrix[ 10 ] * v1_z + this.Matrix[ 14 ] ) / w1;

      decimal w2 = Matrix[ 3 ] * v2_x + Matrix[ 7 ] * v2_y + Matrix[ 11 ] * v2_z + Matrix[ 15 ];
      decimal v2__x = ( Matrix[ 0 ] * v2_x + Matrix[ 4 ] * v2_y + Matrix[ 8 ] * v2_z + Matrix[ 12 ] ) / w2;
      decimal v2__y = ( Matrix[ 1 ] * v2_x + Matrix[ 5 ] * v2_y + Matrix[ 9 ] * v2_z + Matrix[ 13 ] ) / w2;
      decimal v2__z = ( Matrix[ 2 ] * v2_x + Matrix[ 6 ] * v2_y + Matrix[ 10 ] * v2_z + Matrix[ 14 ] ) / w2;

      decimal w3 = Matrix[ 3 ] * v3_x + Matrix[ 7 ] * v3_y + Matrix[ 11 ] * v3_z + Matrix[ 15 ];
      decimal v3__x = ( this.Matrix[ 0 ] * v3_x + Matrix[ 4 ] * v3_y + Matrix[ 8 ] * v3_z + Matrix[ 12 ] ) / w3;
      decimal v3__y = ( Matrix[ 1 ] * v3_x + Matrix[ 5 ] * v3_y + Matrix[ 9 ] * v3_z + Matrix[ 13 ] ) / w3;
      decimal v3__z = ( Matrix[ 2 ] * v3_x + Matrix[ 6 ] * v3_y + Matrix[ 10 ] * v3_z + Matrix[ 14 ] ) / w3;

      Coords.Add( ( decimal )v1__x );
      Coords.Add( ( decimal )v1__y );
      Coords.Add( ( decimal )v1__z );

      Coords.Add( ( decimal )v2__x );
      Coords.Add( ( decimal )v2__y );
      Coords.Add( ( decimal )v2__z );

      Coords.Add( ( decimal )v3__x );
      Coords.Add( ( decimal )v3__y );
      Coords.Add( ( decimal )v3__z );
    }
  }
}
