﻿using AgGateway.ADAPT.ApplicationDataModel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

namespace AgGateway.ADAPT.Plugins
{
    internal class ProductLoader
    {
        private XmlNode _rootNode;
        private string _baseFolder;
        private TaskDataDocument _taskDocument;
        private Dictionary<string, Product> _products;

        private ProductLoader(TaskDataDocument taskDocument)
        {
            _taskDocument = taskDocument;
            _rootNode = _taskDocument.RootNode;
            _baseFolder = _taskDocument.BaseFolder;
            _products = new Dictionary<string, Product>();
        }

        internal static Dictionary<string, Product> Load(TaskDataDocument taskDocument)
        {
            var loader = new ProductLoader(taskDocument);

            return loader.Load();
        }

        private Dictionary<string, Product> Load()
        {
            LoadProducts(_rootNode.SelectNodes("PDT"));
            ProcessExternalNodes();

            return _products;
        }

        private void ProcessExternalNodes()
        {
            var externalNodes = _rootNode.SelectNodes("XFR[starts-with(@A, 'PDT')]");
            foreach (XmlNode externalNode in externalNodes)
            {
                var inputNodes = externalNode.LoadActualNodes("XFR", _baseFolder);
                if (inputNodes == null)
                    continue;
                LoadProducts(inputNodes);
            }
        }

        private void LoadProducts(XmlNodeList inputNodes)
        {
            foreach (XmlNode inputNode in inputNodes)
            {
                string productId;
                var product = LoadProduct(inputNode, out productId);
                if (product != null)
                    _products.Add(productId, product);
            }
        }

        private Product LoadProduct(XmlNode inputNode, out string productId)
        {
            productId = string.Empty;
            if (IsProductMix(inputNode))
                return null;

            var product = GetProduct(inputNode);

            // Required fields. Do not proceed if they are missing
            productId = inputNode.GetXmlNodeValue("@A");
            product.Description = inputNode.GetXmlNodeValue("@B");
            if (productId == null || product.Description == null)
                return null;

            // Optional fields
            LoadQuantity(inputNode, productId);

            _taskDocument.LoadLinkedIds(productId, product.Id);
            return product;
        }

        private Product GetProduct(XmlNode inputNode)
        {
            var product = new FertilizerProduct { ProductType = ProductTypeEnum.Generic, Form = ProductFormEnum.Unknown };

            var groupId = inputNode.GetXmlNodeValue("@C");
            if (string.IsNullOrEmpty(groupId))
                return product;

            var groupNode = _taskDocument.RootNode.SelectSingleNode(
                string.Format(CultureInfo.InvariantCulture, "PGP[@A='{0}']", groupId));
            if (groupNode == null)
                return product;

            var groupType = groupNode.GetXmlNodeValue("@C");
            if (string.IsNullOrEmpty(groupType))
                return product;

            if (string.Equals(groupType, "2", StringComparison.OrdinalIgnoreCase))
                product.ProductType = ProductTypeEnum.Variety;

            return product;
        }

        private static bool IsProductMix(XmlNode inputNode)
        {
            var productType = inputNode.GetXmlNodeValue("@F");

            return !string.IsNullOrEmpty(productType) &&
                string.Equals(productType, "2", StringComparison.OrdinalIgnoreCase) &&
                inputNode.SelectNodes("PRN").Count > 0;
        }

        private void LoadQuantity(XmlNode inputNode, string productId)
        {
            //var valueUnitId = inputNode.GetXmlNodeValue("@D");
            var quantityDdiValue = inputNode.GetXmlNodeValue("@E");

            int quantityDdi = Convert.ToInt32(quantityDdiValue, 16);

            IsoUnit unitOfMeasure = null;
            switch (quantityDdi)
            {
                case 0x48: // DDI 72
                case 0x4B: // DDI 75
                case 0x4E: // DDI 78
                    unitOfMeasure = UnitFactory.Instance.GetUnitByDdi(quantityDdi);
                    break;
            }

            if (unitOfMeasure != null)
                _taskDocument.UnitsByItemId[productId] = unitOfMeasure;
        }
    }
}
