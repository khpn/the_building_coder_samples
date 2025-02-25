#region Header
//
// CopyWallType.cs - duplicate a system type from on project to another to partially transfer project standards
//
// Copyright (C) 2011-2021 by Jeremy Tammik, Autodesk Inc. All rights reserved.
//
// Keywords: The Building Coder Revit API C# .NET add-in.
//
#endregion // Header

#region Namespaces
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
#endregion // Namespaces

namespace BuildingCoder
{
  [Transaction( TransactionMode.Manual )]
  class CmdCopyWallType : IExternalCommand
  {
    /// <summary>
    /// Source project to copy system type from.
    /// </summary>
    const string _source_project_path
      = "Z:/a/case/sfdc/06676034/test/NewWallType.rvt";

    /// <summary>
    /// Source wall type name to copy.
    /// </summary>
    const string _wall_type_name = "NewWallType";

    public Result Execute(
      ExternalCommandData commandData,
      ref string message,
      ElementSet elements )
    {
      UIApplication uiapp = commandData.Application;
      UIDocument uidoc = uiapp.ActiveUIDocument;
      Application app = uiapp.Application;
      Document doc = uidoc.Document;

      // Open source project

      Document docHasFamily = app.OpenDocumentFile(
        _source_project_path );

      // Find system family to copy, e.g. using a named wall type

      WallType wallType = null;

      //WallTypeSet wallTypes = docHasFamily.WallTypes; // 2013

      FilteredElementCollector wallTypes
        = new FilteredElementCollector( docHasFamily ) // 2014
          .OfClass( typeof( WallType ) );

      int i = 0;

      foreach( WallType wt in wallTypes )
      {
        string name = wt.Name;

        Debug.Print( "  {0} {1}", ++i, name );

        if( name.Equals( _wall_type_name ) )
        {
          wallType = wt;
          break;
        }
      }

      if( null == wallType )
      {
        message = string.Format(
          "Cannot find source wall type '{0}'"
          + " in source document '{1}'. ",
          _wall_type_name, _source_project_path );

        return Result.Failed;
      }

      // Create a new wall type in current document

      using( Transaction t = new Transaction( doc ) )
      {
        t.Start( "Transfer Wall Type" );

        WallType newWallType = null;

        //WallTypeSet wallTypes = doc.WallTypes; // 2013 

        wallTypes = new FilteredElementCollector( doc )
          .OfClass( typeof( WallType ) ); // 2014

        foreach( WallType wt in wallTypes )
        {
          if( wt.Kind == wallType.Kind )
          {
            newWallType = wt.Duplicate( _wall_type_name )
              as WallType;

            Debug.Print( string.Format(
              "New wall type '{0}' created.",
              _wall_type_name ) );

            break;
          }
        }

        // Assign parameter values from source wall type:

#if COPY_INDIVIDUAL_PARAMETER_VALUE
    // Example: individually copy the "Function" parameter value:

    BuiltInParameter bip = BuiltInParameter.FUNCTION_PARAM;
    string function = wallType.get_Parameter( bip ).AsString();
    Parameter p = newWallType.get_Parameter( bip );
    p.Set( function );
#endif // COPY_INDIVIDUAL_PARAMETER_VALUE

        Parameter p = null;

        foreach( Parameter p2 in newWallType.Parameters )
        {
          Definition d = p2.Definition;

          if( p2.IsReadOnly )
          {
            Debug.Print( string.Format(
              "Parameter '{0}' is read-only.", d.Name ) );
          }
          else
          {
            p = wallType.get_Parameter( d );

            if( null == p )
            {
              Debug.Print( string.Format(
                "Parameter '{0}' not found on source wall type.",
                d.Name ) );
            }
            else
            {
              if( p.StorageType == StorageType.ElementId )
              {
                // Here you have to find the corresponding
                // element in the target document.

                Debug.Print( string.Format(
                  "Parameter '{0}' is an element id.",
                  d.Name ) );
              }
              else
              {
                if( p.StorageType == StorageType.Double )
                {
                  p2.Set( p.AsDouble() );
                }
                else if( p.StorageType == StorageType.String )
                {
                  p2.Set( p.AsString() );
                }
                else if( p.StorageType == StorageType.Integer )
                {
                  p2.Set( p.AsInteger() );
                }
                Debug.Print( string.Format(
                  "Parameter '{0}' copied.", d.Name ) );
              }
            }
          }

          // Note:
          // If a shared parameter parameter is attached,
          // you need to create the shared parameter first,
          // then copy the parameter value.
        }

        // If the system family type has some other properties,
        // you need to copy them as well here. Reflection can
        // be used to determine the available properties.

        MemberInfo[] memberInfos = newWallType.GetType()
          .GetMembers( BindingFlags.GetProperty );

        foreach( MemberInfo m in memberInfos )
        {
          // Copy the writable property values here.
          // As there are no property writable for
          // Walltype, I ignore this process here.
        }
        t.Commit();
      }
      return Result.Succeeded;
    }


    #region Create Column Types from List of Dimensions
    // shared by Richard @RPThomas108 Thomas in 
    // https://forums.autodesk.com/t5/revit-api-forum/create-columns-types/m-p/10181049

    class ColumnType : EqualityComparer<ColumnType>
    {
      int[] _dim = new int[ 2 ];

      public int H
      {
        get { return _dim[ 0 ]; }
      }

      public int W
      {
        get { return _dim[ 1 ]; }
      }

      public string Name
      {
        get { return H.ToString() + "x" + W.ToString(); }
      }

      public ColumnType( int d1, int d2 )
      {
        if( d1 > d2 )
        {
          _dim = new int[] { d1, d2 };
        }
        else
        {
          _dim = new int[] { d2, d1 };
        }
      }

      public override bool Equals( ColumnType x, ColumnType y )
      {
        return x.H == y.H && x.W == y.W;
      }

      public override int GetHashCode( ColumnType obj )
      {
        return obj.Name.GetHashCode();
      }
    }

    Result CreateColumnTypes( Document doc )
    {
      int[] L1 = new int[] { 100, 200, 150, 500, 400, 300, 250, 250 };
      int[] L2 = new int[] { 200, 200, 150, 500, 400, 300, 250, 250 };

      List<ColumnType> all = new List<ColumnType>();

      for( int i = 0; i < L1.Length; ++i )
      {
        all.Add( new ColumnType( L1[ i ], L2[ i ] ) );
      }

      all = all.Distinct( new ColumnType( 0, 0 ) ).ToList();

      FilteredElementCollector symbols
        = new FilteredElementCollector( doc )
          .WhereElementIsElementType()
          .OfCategory( BuiltInCategory.OST_StructuralColumns );

      // Column name to use

      string column_name = "Concrete-Rectangular-Column";

      IEnumerable<FamilySymbol> existing 
        = symbols.Cast<FamilySymbol>()
          .Where<FamilySymbol>( x 
            => x.FamilyName.Equals( column_name ) ); 

      if( 0 == existing.Count() )
      {
        return Result.Cancelled;
      }

      List<ColumnType> AlreadyExists = new List<ColumnType>();
      List<ColumnType> ToBeMade = new List<ColumnType>();

      for( int i = 0; i < all.Count ; ++i )
      {
        FamilySymbol fs = existing.FirstOrDefault( 
          x => x.Name == all[ i ].Name );

        if( fs == null )
        {
          ToBeMade.Add( all[ i ] );
        }
        else
        {
          AlreadyExists.Add( all[ i ] );
        }
      }
      if( ToBeMade.Count == 0 )
      {
        return Result.Cancelled;
      }

      using( Transaction tx = new Transaction( doc ) )
      {
        if( tx.Start( "Make types" ) == TransactionStatus.Started )
        {
          FamilySymbol first = existing.First();

          foreach( ColumnType ct in ToBeMade )
          {
            ElementType et = first.Duplicate( ct.Name );

            // Use actual type parameter names
            // Use GUIDs instead of LookupParameter where possible

            et.LookupParameter( "h" ).Set( 304.8 * ct.H );
            et.LookupParameter( "b" ).Set( 304.8 * ct.W );
          }
          tx.Commit();
        }
      }
      return Result.Succeeded;
    }
    #endregion // Create Column Types from List of Dimensions
  }
}
