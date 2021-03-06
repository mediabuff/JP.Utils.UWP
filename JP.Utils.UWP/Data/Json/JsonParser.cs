﻿using System;
using Windows.Data.Json;

namespace JP.Utils.Data.Json
{
    public class JsonParser
    {
        /// <summary>
        /// Usage : 
        /// like    var token = JsonParser.GetStringFromJsonObj(json, "token");
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public static string GetStringFromJsonObj(IJsonValue obj, string propertyName, string defaultValue = null)
        {
            try
            {
                if (!obj.GetObject().ContainsKey(propertyName))
                {
                    throw new ArgumentNullException();
                }
                if (obj.GetObject()[propertyName].ValueType == JsonValueType.String)
                {
                    return obj.GetObject()[propertyName].GetString();
                }
                else if (obj.GetObject()[propertyName].ValueType == JsonValueType.Number)
                {
                    return obj.GetObject()[propertyName].GetNumber().ToString();
                }
                else if (obj.GetObject()[propertyName].ValueType == JsonValueType.Boolean)
                {
                    return obj.GetObject()[propertyName].GetBoolean() ? "true" : "false";
                }
                else throw new Exception();
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        public static double GetNumberFromJsonObj(IJsonValue obj, string propertyName, double defaultValue = 0)
        {
            try
            {
                if (!obj.GetObject().ContainsKey(propertyName))
                {
                    throw new ArgumentNullException();
                }
                return obj.GetObject()[propertyName].GetNumber();
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        public static double? GetNullableNumberFromJsonObj(IJsonValue obj, string propertyName, double? defaultValue = null)
        {
            try
            {
                if (!obj.GetObject().ContainsKey(propertyName))
                {
                    throw new ArgumentNullException();
                }
                return obj.GetObject()[propertyName].GetNumber();
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        public static bool GetBooleanFromJsonObj(IJsonValue obj, string propertyName, bool defaultValue = false)
        {
            try
            {
                if (!obj.GetObject().ContainsKey(propertyName))
                {
                    throw new ArgumentNullException();
                }
                return obj.GetObject()[propertyName].GetBoolean();
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        public static JsonArray GetJsonArrayFromJsonObj(IJsonValue obj, string propertyName)
        {
            try
            {
                if (!obj.GetObject().ContainsKey(propertyName))
                {
                    throw new ArgumentNullException();
                }
                return obj.GetObject()[propertyName].GetArray();
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static JsonObject GetJsonObjFromJsonObj(IJsonValue obj, string propertyName)
        {
            try
            {
                if (!obj.GetObject().ContainsKey(propertyName))
                {
                    throw new ArgumentNullException();
                }
                return obj.GetObject()[propertyName].GetObject();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
