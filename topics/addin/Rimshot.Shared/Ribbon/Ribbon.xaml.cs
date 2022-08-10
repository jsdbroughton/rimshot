﻿using Autodesk.Navisworks.Api.Plugins;
using System.Text;
using System.Windows.Forms;
using NavisworksApp = Autodesk.Navisworks.Api.Application;

namespace Rimshot {

  [Plugin( "Rimshot", "Rimshot", DisplayName = "Rimshot" )]
  [Strings( "Ribbon.name" )]
  [RibbonLayout( "Ribbon.xaml" )]
  [RibbonTab( "Rimshot", DisplayName = "Rimshot", LoadForCanExecute = true )]

  [Command( Rimshot.Command,
             CallCanExecute = CallCanExecute.DocumentNotClear,
             Icon = "icon_register16.ico",
             LargeIcon = "icon_register32.ico",
             Shortcut = "Ctrl+Shift+R",
             ShortcutWindowTypes = "",
             ToolTip = "Show Rimshot ",
             ExtendedToolTip = "Show Rimshot Issue List",
             DisplayName = "Register"
             )]

  [Command( Speckle.Command,
             CallCanExecute = CallCanExecute.DocumentNotClear,
             Icon = "icon_speckle16.ico",
             LargeIcon = "icon_speckle32.ico",
             Shortcut = "Ctrl+Shift+S",
             ShortcutWindowTypes = "",
             ToolTip = "Show Speckle Connector",
             ExtendedToolTip = "Show Speckle Connector",
             DisplayName = "Speckle"
             )]


  class RibbonHandler : CommandHandlerPlugin {
    public override CommandState CanExecuteCommand ( string commandId ) {

      CommandState state = new CommandState( true );

      switch ( commandId ) {
        case ShowOnly.Command: {

            if ( NavisworksApp.ActiveDocument.CurrentSelection.IsEmpty ) {
              state.IsEnabled = false;
            }

            break;
          }

        case ShowAlso.Command: {

            if ( NavisworksApp.ActiveDocument.CurrentSelection.IsEmpty ) {
              state.IsEnabled = false;
            }

            break;
          }
      }



      return state;


    }

    public bool LoadPlugin ( string plugin, bool notAutomatedCheck = true, string command = "" ) {
      if ( notAutomatedCheck && NavisworksApp.IsAutomated ) {
        return false;
      }

      if ( plugin.Length == 0 || command.Length == 0 ) {
        return false;
      }

      PluginRecord pluginRecord = NavisworksApp.Plugins.FindPlugin( plugin + ".Rimshot" );

      if ( pluginRecord is null ) {
        return false;
      }

      Plugin loadedPlugin = pluginRecord.LoadedPlugin ?? pluginRecord.LoadPlugin();

      // Activate the Plugin pane if it is of the right type
      if ( pluginRecord.IsLoaded && pluginRecord is DockPanePluginRecord && pluginRecord.IsEnabled ) {
        DockPanePlugin dockPanePlugin = ( DockPanePlugin )loadedPlugin;
        dockPanePlugin.ActivatePane();
      } else {
#if DEBUG
        StringBuilder sb = new StringBuilder();

        foreach ( PluginRecord pr in NavisworksApp.Plugins.PluginRecords ) {
          sb.AppendLine( pr.Name + ": " + pr.DisplayName + ", " + pr.Id );
        }

        MessageBox.Show( sb.ToString() );
        MessageBox.Show( command + " Plugin not loaded." );
#endif
      }

      return pluginRecord.IsLoaded;
    }

    public override int ExecuteCommand ( string commandId, params string[] parameters ) {
#if IS2018
      string buildVersion = "2018";
#endif
#if IS2019
      string buildVersion = "2019";
#endif
#if IS2020
      string buildVersion = "2020";
#endif
#if IS2021
      string buildVersion = "2021";
#endif
#if IS2022
      string buildVersion = "2022";
#endif
#if IS2023
      string buildVersion = "2023";
#endif

      // Version
      if ( !NavisworksApp.Version.RuntimeProductName.Contains( buildVersion ) ) {
        MessageBox.Show( "This Add-In was built for Navisworks " + buildVersion + ", please contact r+d@rimshot.com for assistance...",
                        "Cannot Continue!",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error );
        return 0;
      }

      switch ( commandId ) {

        // Show Only Selected Elements
        case ShowOnly.Command: {
            Selections.ShowSelected( showOnlySelected: true );
            break;
          }
        // Show Only Selected Elements
        case ShowAlso.Command: {
            Selections.ShowSelected();
            break;
          }

        // This is a pane based tool
        case Rimshot.Command: {
            LoadPlugin( plugin: Rimshot.Plugin, command: commandId );
            break;
          }

        // This is a pane based tool
        case Speckle.Command: {
            LoadPlugin( plugin: Speckle.Plugin, command: commandId );
            break;
          }

        default: {
            MessageBox.Show( "You have clicked on the command with ID = '" + commandId + "'" );
            break;
          }
      }

      return 0;
    }
  }
}