﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Umbraco.Core;
using umbraco.BusinessLogic.Actions;
using umbraco.interfaces;

namespace Umbraco.Web.Trees.Menu
{

    [DataContract(Name = "menuItems", Namespace = "")]
    public class MenuItemCollection
    {
        private readonly string _packageFolderName;
        private readonly List<MenuItem> _menuItems;
        
        public MenuItemCollection()
        {
            _menuItems = new List<MenuItem>();
        }

        public MenuItemCollection(IEnumerable<MenuItem> items)
        {
            _menuItems = new List<MenuItem>(items);
        }

        public MenuItemCollection(string packageFolderName)
            : this()
        {
            _packageFolderName = packageFolderName;
        }

        public MenuItemCollection(string packageFolderName, IEnumerable<MenuItem> items)
        {
            _packageFolderName = packageFolderName;
            _menuItems = new List<MenuItem>(items);
        }

        /// <summary>
        /// Sets the default menu item alias to be shown when the menu is launched - this is optional and if not set then the menu will just be shown normally.
        /// </summary>
        [DataMember(Name = "defaultAlias")]
        public string DefaultMenuAlias { get; set; }

        /// <summary>
        /// The list of menu items
        /// </summary>
        [DataMember(Name = "menuItems")]
        public IEnumerable<MenuItem> MenuItems
        {
            get { return _menuItems; }
        }

        /// <summary>
        /// Adds a menu item
        /// </summary>
        /// <param name="action"></param>
        /// <param name="name">The text to display for the menu item, will default to the IAction alias if not specified</param>
        internal MenuItem AddMenuItem(IAction action, string name)
        {
            var item = new MenuItem(action);

            DetectLegacyActionMenu(action.GetType(), item);

            AddItemToCollection(item);
            return item;
        }

        /// <summary>
        /// Removes a menu item
        /// </summary>
        /// <param name="item"></param>
        public void RemoveMenuItem(MenuItem item)
        {
            _menuItems.Remove(item);
        }

        /// <summary>
        /// Adds a menu item
        /// </summary>
        public void AddMenuItem(MenuItem item)
        {
            AddItemToCollection(item);
        }
        
        /// <summary>
        /// Adds a menu item
        /// </summary>
        /// <typeparam name="TMenuItem"></typeparam>
        /// <typeparam name="TAction"></typeparam>
        /// <param name="hasSeparator"></param>
        /// <param name="name">The text to display for the menu item, will default to the IAction alias if not specified</param>
        /// <param name="additionalData"></param>
        /// <returns></returns>
        public TMenuItem AddMenuItem<TMenuItem, TAction>(string name, bool hasSeparator = false, IDictionary<string, object> additionalData = null)
            where TAction : IAction
            where TMenuItem : MenuItem, new()
        {
            var item = CreateMenuItem<TAction>(name, hasSeparator, additionalData);
            if (item == null) return null;

            var customMenuItem = new TMenuItem
                {
                    Name = item.Alias,
                    Alias = item.Alias,
                    SeperatorBefore = hasSeparator,
                    Icon = item.Icon,
                    Action = item.Action
                };

            AddItemToCollection(customMenuItem);

            return customMenuItem;
        }

        /// <summary>
        /// Adds a menu item
        /// </summary>
        /// <param name="name">The text to display for the menu item, will default to the IAction alias if not specified</param>
        /// <typeparam name="T"></typeparam>
        public MenuItem AddMenuItem<T>(string name)
            where T : IAction
        {
            return AddMenuItem<T>(name, false, null);
        }

        /// <summary>
        /// Adds a menu item with a key value pair which is merged to the AdditionalData bag
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="name">The text to display for the menu item, will default to the IAction alias if not specified</param>
        /// <param name="hasSeparator"></param>
        public MenuItem AddMenuItem<T>(string name, string key, string value, bool hasSeparator = false)
            where T : IAction
        {
            return AddMenuItem<T>(name, hasSeparator, new Dictionary<string, object> { { key, value } });
        }

        /// <summary>
        /// Adds a menu item with a dictionary which is merged to the AdditionalData bag
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="hasSeparator"></param>
        /// /// <param name="name">The text to display for the menu item, will default to the IAction alias if not specified</param>
        /// <param name="additionalData"></param>
        public MenuItem AddMenuItem<T>(string name, bool hasSeparator = false, IDictionary<string, object> additionalData = null)
            where T : IAction
        {
            var item = CreateMenuItem<T>(name, hasSeparator, additionalData);
            if (item != null) 
            {
                AddItemToCollection(item);
                return item;
            }
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="hasSeparator"></param>
        /// <param name="name">The text to display for the menu item, will default to the IAction alias if not specified</param>
        /// <param name="additionalData"></param>
        /// <returns></returns>
        internal MenuItem CreateMenuItem<T>(string name, bool hasSeparator = false, IDictionary<string, object> additionalData = null)
            where T : IAction
        {
            var item = ActionsResolver.Current.GetAction<T>();
            if (item != null)
            {
                var menuItem = new MenuItem(item, name)
                {
                    SeperatorBefore = hasSeparator
                };

                if (additionalData != null)
                {
                    foreach (var i in additionalData)
                    {
                        menuItem.AdditionalData[i.Key] = i.Value;
                    }
                }

                DetectLegacyActionMenu(typeof(T), menuItem);

                //TODO: Once we implement 'real' menu items, not just IActions we can implement this since
                // people may need to pass specific data to their menu items

                ////validate the data in the meta data bag
                //item.ValidateRequiredData(AdditionalData);

                return menuItem;
            }
            return null;
        }

        /// <summary>
        /// Checks if the IAction type passed in is attributed with LegacyActionMenuItemAttribute and if so 
        /// ensures that the correct action metadata is added.
        /// </summary>
        /// <param name="actionType"></param>
        /// <param name="menuItem"></param>
        private void DetectLegacyActionMenu(Type actionType, MenuItem menuItem)
        {
            //This checks for legacy IActions that have the LegacyActionMenuItemAttribute which is a legacy hack
            // to make old IAction actions work in v7 by mapping to the JS used by the new menu items
            var attribute = actionType.GetCustomAttribute<LegacyActionMenuItemAttribute>(false);
            if (attribute != null)
            {
                //add the current type to the metadata
                if (attribute.MethodName.IsNullOrWhiteSpace())
                {
                    //if no method name is supplied we will assume that the menu action is the type name of the current menu class
                    menuItem.AdditionalData.Add("jsAction", string.Format("{0}.{1}", attribute.ServiceName, this.GetType().Name));
                }
                else
                {
                    menuItem.AdditionalData.Add("jsAction", string.Format("{0}.{1}", attribute.ServiceName, attribute.MethodName));
                }
            }
        }

        /// <summary>
        /// This handles adding a menu item to the internal collection and will configure it accordingly
        /// </summary>
        /// <param name="menuItem"></param>
        private void AddItemToCollection(MenuItem menuItem)
        {
            if (_packageFolderName.IsNullOrWhiteSpace() == false)
            {
                menuItem.SetPackageFolder(_packageFolderName);
            }
            _menuItems.Add(menuItem);
        }

    }
}