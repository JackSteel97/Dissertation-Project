var app = new Vue({
    el: '#home-app',
    data: {
        loading: false,
        loadingCongestionViz: false,
        backgroundLayer: null,

        locationOptions: [
            {
                group: 'startSet',
                children: [],
                name: 'Set Start Point'
            },
            {
                group: 'buildings',
                children: [],
                name: 'Buildings'
            }
        ],

        startingTime: {
            selectedTime: null,
            config: {
                wrap: true,
                dateFormat: 'H:i',
                enableTime: true,
                noCalendar: true,
                time_24hr: true
            }
        },

        routePlanner: {
            endPoints: [],
            startPoints: [],
            selectedDestination: null,
            selectedStart: null
        },

        routeRequest: {
            endPoint: null,
            startPoint: null
        },

        routeDetails: {
            totalDistance: '',
            walkingTime: ''
        },

        adjustedRouteDetails: {
            totalDistance: '',
            walkingTime: ''
        },

        route: {
            buildingsInvolved: [],
            drawableRoutes: [],
            currentRouteSection: 0,
            adjustedDrawableRoutes: []
        },

        leafletMap: {
            map: null,
            indoorLayers: [],
            routeLine: null,
            maxBounds: null,
            routeLineArrow: null,
            adjustedRouteLine: null,
            adjustedRouteLineArrow: null,
            currentLevel: 0
        },

        congestionViz: {
            visible: false,
            allCorridors: [],
            currentCongestionValues: [],
            maxCongestionValue: 0,
            heatLayer: null
        }
    },

    methods: {
        getAllCorridors() {
            fetch(`${window.location.origin}/campusdata/corridors`, {
                method: 'get',
                headers: {
                    "Content-type": "application/json; charset=UTF-8"
                }
            }).then((data) => {
                if (data.ok) {
                    data.json().then(result => {
                        this.congestionViz.allCorridors = result;
                    });
                } else {
                    // Something went wrong.
                    data.text().then(result => {
                        this.$bvToast.toast(result, {
                            title: 'Get Corridors Error',
                            autoHideDelay: 30000,
                            variant: 'danger'
                        });
                    });
                }
            }).catch((error) => {
                console.error(error);
            });
        },

        clearCongestionViz() {
            if (this.congestionViz.heatLayer) {
                this.leafletMap.map.removeLayer(this.congestionViz.heatLayer);
            }
        },

        displayCongestionHeatmap(level) {
            let heatPoints = [];

            for (let [key, value] of Object.entries(this.congestionViz.currentCongestionValues)) {
                let nodes = key.split(',');

                for (let i = 0; i < nodes.length; i++) {
                    // Find this node.
                    let currentNode = this.congestionViz.allCorridors.find(node => node.nodeId === nodes[i]);

                    if (currentNode && currentNode.floor === level) {
                        heatPoints.push([currentNode.latitude, currentNode.longitude, value]);
                    }
                }
            }

            this.congestionViz.heatLayer = L.heatLayer(heatPoints, {
                max: this.congestionViz.maxCongestionValue,
                minOpacity: 0.7,
                radius: 20,
                blur: 15,
                gradient: { 0.4: 'blue', 0.65: 'lime', 1: 'red' }
            }).addTo(this.leafletMap.map);
        },

        toggleCongestionViz() {
            this.congestionViz.visible = !this.congestionViz.visible;
            if (this.congestionViz.visible) {
                this.displayCongestionHeatmap(this.leafletMap.currentLevel);
            } else {
                this.clearCongestionViz();
            }
            this.loadBackgroundMap(this.congestionViz.visible);
        },

        initialiseLeafletMap() {
            // Set bounds for map movement.
            let southWest = L.latLng(53.2236339, -0.5636355);
            let northEast = L.latLng(53.233065, -0.5367614);
            this.leafletMap.mapMaxBounds = L.latLngBounds(southWest, northEast);

            // Initialise the map - L = Leaflet Library.
            this.leafletMap.map = new L.map('map', { maxZoom: 22, minZoom: 16, maxBounds: this.leafletMap.maxBounds }).setView([53.22832837650, -0.54774213367], 17);

            this.loadBackgroundMap();
        },

        loadBackgroundMap(forCongestionView = false) {
            if (this.backgroundLayer) {
                this.leafletMap.map.removeLayer(this.backgroundLayer);
            }
            // Load background map.
            fetch(`${window.location.origin}/campusdata/map/backgroundMap.json`, {
                method: 'get',
                headers: {
                    "Content-type": "application/json; charset=UTF-8"
                }
            }).then((data) => {
                if (data.ok) {
                    data.json().then((result) => {
                        this.backgroundLayer = L.geoJson(result, {
                            onEachFeature: (feature, layer) => {
                                // Does it have a name?
                                if (feature.properties.name) {
                                    // Bind a popup for the name.
                                    layer.bindPopup(feature.properties.name.toString());
                                }
                            },
                            style: (feature) => {
                                let fillColor,
                                    building = feature.properties.building,
                                    colour = feature.properties.campusColour;
                                let chosenWeight = 0;
                                if (building === 'yes') {
                                    fillColor = '#e0e6ee';
                                }
                                else {
                                    // No data.
                                    fillColor = '#ffffff';
                                }

                                if (colour) {
                                    fillColor = colour;
                                }

                                if (fillColor.toLowerCase() === "#ffffff" || forCongestionView) {
                                    chosenWeight = 1;
                                }

                                return { color: '#000000', weight: chosenWeight, fillColor: fillColor, fillOpacity: forCongestionView ? 0.05 : 0.9 };
                            }
                        }).addTo(this.leafletMap.map).bringToBack();
                    });
                } else {
                    data.text().then((result) => {
                        // Something went wrong.
                        this.$bvToast.toast(result, {
                            title: 'Error',
                            autoHideDelay: 30000,
                            variant: 'danger'
                        });
                    });
                }
            }).catch((error) => {
                console.error(error);
            });
        },
        clearAllIndoorLayers() {
            this.leafletMap.indoorLayers.forEach(layer => {
                this.leafletMap.map.removeLayer(layer);
            });

            this.leafletMap.indoorLayers = [];

            if (this.leafletMap.routeLine) {
                this.leafletMap.map.removeLayer(this.leafletMap.routeLine);
            }

            if (this.leafletMap.routeLineArrow) {
                this.leafletMap.map.removeLayer(this.leafletMap.routeLineArrow);
            }
        },

        leafletLoadIndoorLayer(indoorFileToLoad) {
            // Read and add indoor layer to map.
            fetch(`${window.location.origin}/campusdata/map/${indoorFileToLoad}`, {
                method: 'get',
                headers: {
                    "Content-type": "application/json; charset=UTF-8"
                }
            }).then((data) => {
                if (data.ok) {
                    data.json().then((result) => {
                        let indoorLayer = L.geoJson(result, {
                            onEachFeature: (feature, layer) => {
                                // Does it have a name?
                                if (feature.properties.name) {
                                    // Bind a popup for the name.
                                    layer.bindPopup(feature.properties.name.toString());
                                }
                            },
                            style: (feature) => {
                                let fill = 'white';
                                let borderColour = '#ffffff';
                                // Set colour style for feature.
                                if (feature.properties.node_type.toLowerCase() === 'block') {
                                    fill = '#666465';
                                    borderColour = '#343233';
                                }
                                else if (feature.properties.node_type.toLowerCase() === 'room') {
                                    fill = '#012c61';
                                }
                                else if (feature.properties.node_type.toLowerCase() === 'stairs' || feature.properties.node_type.toLowerCase() === 'lift') {
                                    fill = '#199bcc';
                                }
                                else if (feature.properties.node_type.toLowerCase() === 'other') {
                                    fill = '#cdcccc';
                                }
                                else if (feature.properties.node_type.toLowerCase().indexOf('wc') !== -1) {
                                    fill = '#ec008c';
                                }

                                return {
                                    fillColor: fill,
                                    color: borderColour,
                                    weight: 1,
                                    fillOpacity: 1
                                };
                            }
                        }).addTo(this.leafletMap.map).bringToFront();
                        this.leafletMap.indoorLayers.push(indoorLayer);
                    });
                } else {
                    data.text().then((result) => {
                        // Something went wrong.
                        // Probably there isn't an indoor file for this building. This is usually fine.
                        console.warn(result);
                    });
                }
            }).catch((error) => {
                console.error(error);
            });
        },

        loadLevelsForRoute(route) {
            if (route.length > 0) {
                this.clearAllIndoorLayers();
                // Get the floor number from the node id.
                let level = route[0].floor;

                // Set current displayed level.
                this.leafletMap.currentLevel = level;

                if (this.congestionViz.visible) {
                    this.displayCongestionHeatmap(this.leafletMap.currentLevel);
                }

                // Load that floor for all involved buildings.
                this.route.buildingsInvolved.forEach(building => {
                    this.leafletLoadIndoorLayer(`${building}${level}rooms.json`);
                });
            }
        },

        leafletRemoveRoute() {
            // If a route already exists on the map, remove it.
            if (this.leafletMap.routeLine) {
                this.leafletMap.map.removeLayer(this.leafletMap.routeLine);
                this.leafletMap.map.removeLayer(this.leafletMap.routeLineArrow);
            }

            if (this.leafletMap.adjustedRouteLine) {
                this.leafletMap.map.removeLayer(this.leafletMap.adjustedRouteLine);
                this.leafletMap.map.removeLayer(this.leafletMap.adjustedRouteLineArrow);
            }
        },

        leafletDrawRoute(route, routeColor = 'green') {
            let routeLine, routeLineArrow;

            if (route) {
                // Loop through the route received, extract latitude & longitude and add them to a list.
                let pointList = route.map(node => {
                    return new L.LatLng(node.latitude, node.longitude);
                });

                if (pointList.length > 1) {
                    // Draw route line using the list of points and add to map.
                    routeLine = new L.Polyline(pointList, {
                        color: routeColor,
                        weight: 4,
                        opacity: 1,
                        smoothFactor: 1
                    });

                    routeLine.addTo(this.leafletMap.map).bringToFront();

                    let arrowOptions = {
                        distanceUnit: 'km',
                        isWindDegree: false,
                        stretchFactor: 1,
                        arrowheadLength: 0.001,
                        color: routeColor,
                        opacity: 1
                    };

                    let secondLastPoint = pointList[pointList.length - 2];
                    let lastPoint = pointList[pointList.length - 1];

                    let arrowData = {
                        latlng: secondLastPoint,
                        degree: calculateBearingFromLatLonPoints(secondLastPoint.lat, secondLastPoint.lng, lastPoint.lat, lastPoint.lng),
                        distance: getDistanceFromLatLonInMeters(secondLastPoint.lat, secondLastPoint.lng, lastPoint.lat, lastPoint.lng) / 1000
                    };

                    routeLineArrow = new L.Arrow(arrowData, arrowOptions);
                    routeLineArrow.addTo(this.leafletMap.map).bringToFront();
                }
            }
            return { routeLine, routeLineArrow };
        },

        leafletFitBounds(bounds) {
            this.leafletMap.map.setZoomAround(bounds.getCenter(), 19, { animate: false });
            this.leafletMap.map.fitBounds(bounds, { duration: 0 });
        },

        destinationChanged(newValue) {
            // Copy the object to avoid changing the select box options when changing the route request.
            this.routeRequest.endPoint = Object.assign({}, newValue);
        },
        startChanged(newValue) {
            this.routeRequest.startPoint = Object.assign({}, newValue);
        },

        getRoute() {
            this.loading = true;

            if (this.routeRequest.endPoint && this.routeRequest.startPoint) {
                this.clearCongestionViz();
                // Both selected, start routing.
                this.calculateRoute();
            } else {
                this.$bvToast.toast('You must select both a start and destination.', {
                    title: 'Error',
                    autoHideDelay: 30000,
                    variant: 'danger'
                });
            }
        },

        resetRouteVariables() {
            this.route.buildingsInvolved = [];
            this.route.drawableRoutes = [];
            this.route.currentRouteSection = 0;
        },

        getWalkingTimeAndDistance(route) {
            let walkingTime = '';
            let totalDistance = '';

            let walkingDuration = route.walkingTimeSeconds;
            let mins = 0;

            if (walkingDuration > 60) {
                mins = Math.floor(walkingDuration / 60);
                walkingTime += `${mins} minute${mins > 1 ? 's' : ''} `;
            }

            let secs = walkingDuration - (60 * mins);
            walkingTime += `${secs} seconds`;

            if (route.totalDistance > 0) {
                if (route.totalDistance > 1000) {
                    totalDistance = `${(route.totalDistance / 1000).toFixed(2)}km`;
                } else {
                    totalDistance = `${Math.round(route.totalDistance)}m`;
                }
            }

            return { walkingTime, totalDistance };
        },

        incrementRouteSection() {
            let newSection = this.route.currentRouteSection + 1;
            console.log(newSection, this.route.drawableRoutes.length);
            if (newSection < this.route.drawableRoutes.length) {
                this.jumpToRouteSection(newSection);
            }
        },

        decrementRouteSection() {
            let newSection = this.route.currentRouteSection - 1;
            if (newSection >= 0) {
                this.jumpToRouteSection(newSection);
            }
        },

        jumpToRouteSection(sectionIndex) {
            if (sectionIndex >= 0) {
                this.route.currentRouteSection = sectionIndex;

                // Load the new floors on the new route.
                this.loadLevelsForRoute(this.route.drawableRoutes[this.route.currentRouteSection]);

                // Clear existing routes.
                this.leafletRemoveRoute();

                // Draw the new route.
                let normalLines = this.leafletDrawRoute(this.route.drawableRoutes[this.route.currentRouteSection]);

                this.leafletMap.routeLine = normalLines.routeLine;
                this.leafletMap.routeLineArrow = normalLines.routeLineArrow;

                // Draw the next section of the adjusted route.
                // Assuming it has a corresponding section.
                if (this.route.currentRouteSection < this.route.adjustedDrawableRoutes.length) {
                    let adjustedLines = this.leafletDrawRoute(this.route.adjustedDrawableRoutes[this.route.currentRouteSection], 'orange');

                    this.leafletMap.adjustedRouteLine = adjustedLines.routeLine;
                    this.leafletMap.adjustedRouteLineArrow = adjustedLines.routeLineArrow;
                }

                // Centre on first node.
                centerLat = this.route.drawableRoutes[this.route.currentRouteSection][0].latitude;
                centerLon = this.route.drawableRoutes[this.route.currentRouteSection][0].longitude;
                this.leafletMap.map.panTo(new L.LatLng(centerLat, centerLon), 8);
            }
        },

        calculateRoute() {
            this.clearAllIndoorLayers();
            this.leafletRemoveRoute();
            this.route.drawableRoutes = [];
            this.routeDetails.totalDistance = 0;
            this.adjustedRouteDetails.totalDistance = 0;

            let startNodeId = this.routeRequest.startPoint.code;
            let endNodeId = this.routeRequest.endPoint.code;

            // Work out the route type.
            let routeType = `${this.routeRequest.startPoint.type[0].toLowerCase()}2${this.routeRequest.endPoint.type[0].toLowerCase()}`;

            fetch(`${window.location.origin}/routing/routerequest/${startNodeId}/${endNodeId}/${routeType}/${this.startingTime.selectedTime}`, {
                method: 'get',
                headers: {
                    "Content-type": "application/json; charset=UTF-8"
                }
            }).then((data) => {
                if (data.ok) {
                    data.json().then(({ normalRoute, adjustedRoute }) => {
                        if (normalRoute.routeNodes && normalRoute.routeNodes.length > 0) {
                            let normalMeasurements = this.getWalkingTimeAndDistance(normalRoute);
                            this.routeDetails.walkingTime = normalMeasurements.walkingTime;
                            this.routeDetails.totalDistance = normalMeasurements.totalDistance;

                            let adjustedMeasurements = this.getWalkingTimeAndDistance(adjustedRoute);
                            this.adjustedRouteDetails.walkingTime = adjustedMeasurements.walkingTime;
                            this.adjustedRouteDetails.totalDistance = adjustedMeasurements.totalDistance;

                            // Get rid of the last node.
                            normalRoute.routeNodes.pop();
                            adjustedRoute.routeNodes.pop();

                            // Get congestion prediction.
                            this.congestionViz.currentCongestionValues = normalRoute.congestion;
                            // Get max value.
                            this.congestionViz.maxCongestionValue = normalRoute.maxCongestion;

                            this.parseRoute(normalRoute.routeNodes, adjustedRoute.routeNodes);
                        } else {
                            this.$bvToast.toast('No route found.', {
                                title: 'Error',
                                autoHideDelay: 30000,
                                variant: 'danger'
                            });
                        }
                    });
                } else {
                    data.text().then((result) => {
                        // Something went wrong.
                        this.$bvToast.toast(result, {
                            title: 'Error',
                            autoHideDelay: 30000,
                            variant: 'danger'
                        });
                    });
                }
            }).catch((error) => {
                console.error(error);
            }).finally(() => {
                this.loading = false;
            });
        },

        extractDrawableRoutes(route) {
            // Verify route.
            route = route.filter(r => r.type !== NodeType.Room);

            let currentLevel = 0;
            let drawableRoutes = [];

            if (route.length === 0) {
                return drawableRoutes;
            }

            // Initialise current level to first node of route.
            currentLevel = route[0].floor;

            // Initialise a temporary holder.
            let tempRoute = [];

            // Iterate through each node in the route.
            route.forEach(node => {
                // If the node is a corridor node.
                if (node.type === NodeType.Corridor || node.type === NodeType.Parking) {
                    // Add to buildings involved in the route.
                    this.addToBuildingsInvolved(node.buildingCode);

                    // Get the level of this node.
                    let nodeLevel = node.floor;

                    // If the level is different to the current level (level of previous node).
                    if (nodeLevel !== currentLevel) {
                        // This is a new floor.
                        // Add the route for the current floor to drawable routes - it is finished.
                        drawableRoutes.push(tempRoute);
                        // Reset the temporary holder.
                        tempRoute = [];
                        // Move the current level to the next floor.
                        currentLevel = nodeLevel;
                    }

                    // Add this node to the temporary holder.
                    tempRoute.push(node);
                }
            });

            // Add last route to drawable routes.
            drawableRoutes.push(tempRoute);

            return drawableRoutes;
        },

        parseRoute(route, adjustedRoute) {
            // Clear any pre-existing routes and buildings.
            this.resetRouteVariables();

            this.route.drawableRoutes = this.extractDrawableRoutes(route);
            this.route.adjustedDrawableRoutes = this.extractDrawableRoutes(adjustedRoute);

            // Load floors for the first route.
            this.loadLevelsForRoute(this.route.drawableRoutes[0]);

            // Display success message.
            this.$bvToast.toast('Route Found!', {
                title: 'Success',
                autoHideDelay: 10000,
                variant: 'success'
            });

            // Get bounds of route box from start and end. Used to zoom the map to display both the start and end points.
            let firstNode = this.route.drawableRoutes[0][0];
            let lastNode = this.route.drawableRoutes[0][this.route.drawableRoutes[0].length - 1];
            let start = new L.LatLng(firstNode.latitude, firstNode.longitude);
            let end = new L.LatLng(lastNode.latitude, lastNode.longitude);

            let routeBounds = L.latLngBounds([start, end]);

            // Clear existing routes.
            this.leafletRemoveRoute();

            // Draw first route.
            let normalLines = this.leafletDrawRoute(this.route.drawableRoutes[0]);

            this.leafletMap.routeLine = normalLines.routeLine;
            this.leafletMap.routeLineArrow = normalLines.routeLineArrow;

            let adjustedLines = this.leafletDrawRoute(this.route.adjustedDrawableRoutes[0], 'orange');

            this.leafletMap.adjustedRouteLine = adjustedLines.routeLine;
            this.leafletMap.adjustedRouteLineArrow = adjustedLines.routeLineArrow;

            this.leafletFitBounds(routeBounds);
        },

        addToBuildingsInvolved(buildingCode) {
            // If the building code has not already been added.
            if (!~this.route.buildingsInvolved.findIndex((b) => { return b.toLowerCase() === buildingCode.toLowerCase(); })) {
                // If the building code is not the code for outdoors.
                if (buildingCode !== 'out') {
                    // Add it to the array.
                    this.route.buildingsInvolved.push(buildingCode);
                }
            }
        },

        cacheLocationData() {
            let serialisedLocations = JSON.stringify({
                locations: this.locationOptions,
                timestamp: moment().valueOf()
            });
            localStorage['navigation-locations'] = serialisedLocations;
        },

        tryGetLocationsFromStorage() {
            // Get data from localStorage.
            let serialisedLocations = localStorage['navigation-locations'];
            // Check it exists.
            if (serialisedLocations) {
                // Parse.
                let locationsData = JSON.parse(serialisedLocations);
                if (locationsData.timestamp) {
                    // Check if the data has expired.
                    let expiration = moment(locationsData.timestamp);
                    expiration.add(1, 'hours');
                    if (expiration.isAfter(moment())) {
                        // Not expired, valid, use it.
                        this.locationOptions = locationsData.locations;
                        return true;
                    }
                }
            }
            // Needs refresh.
            return false;
        },

        getSelectBoxData() {
            let buildingDone = false;
            let roomsDone = false;
            this.loading = true;

            if (this.tryGetLocationsFromStorage()) {
                this.loading = false;
                return;
            }

            // Buildings.
            fetch(`${window.location.origin}/campusdata/buildings`, {
                method: 'get',
                headers: {
                    "Content-type": "application/json; charset=UTF-8"
                }
            }).then((data) => {
                if (data.ok) {
                    data.json().then((result) => {
                        // Find buildings index.
                        let buildingsIndex = this.locationOptions.findIndex(group => group.group && group.group.toLowerCase() === 'buildings');

                        // Extract the building data.
                        result.forEach(buildingEntry => {
                            const code = buildingEntry.buildingCode;
                            const name = CapitaliseWords(buildingEntry.buildingName).trim();

                            this.locationOptions[buildingsIndex].children.push({ name: name, code: code, type: 'building' });
                        });

                        buildingDone = true;
                        if (roomsDone) {
                            this.cacheLocationData();
                            this.loading = false;
                        }
                    });
                } else {
                    data.text().then((result) => {
                        // Something went wrong.
                        this.$bvToast.toast(result, {
                            title: 'Error',
                            autoHideDelay: 30000,
                            variant: 'danger'
                        });
                    });
                }
            }).catch((error) => {
                console.error(error);
            });

            // Rooms.
            fetch(`${window.location.origin}/campusdata/rooms`, {
                method: 'get',
                headers: {
                    "Content-type": "application/json; charset=UTF-8"
                }
            }).then((data) => {
                if (data.ok) {
                    data.json().then((result) => {
                        let buildings = [];

                        // Iterate through response data.
                        result.forEach(roomEntry => {
                            // Does the building already have a group?
                            if (buildings.indexOf(roomEntry.nodeBuildingCode) < 0) {
                                // No, create the group first.
                                buildings.push(roomEntry.nodeBuildingCode);

                                // Get building data.
                                const code = roomEntry.nodeBuildingCode;
                                const name = CapitaliseWords(roomEntry.buildingName);

                                this.locationOptions.push({
                                    code: code,
                                    group: `${roomEntry.buildingName} ${code}`,
                                    name: `${name} Rooms (${code.toUpperCase()})`,
                                    children: []
                                });
                            }

                            let txt = '';
                            const nodeName = roomEntry.roomName;
                            const nodeId = roomEntry.nodeId;

                            if (nodeName) {
                                // Use name.
                                txt = nodeName;
                            } else {
                                // Split string at '_' and create user friendly room code.
                                let arr = nodeId.split('_');
                                if (arr.length > 2) {
                                    txt = (arr[1] + arr[2]).toLowerCase();
                                }
                            }

                            // Get this nodes building group.
                            const buildingCode = roomEntry.nodeBuildingCode;

                            // Find the group.
                            let groupIndex = this.locationOptions.findIndex(group => group.code && group.code.toLowerCase() === buildingCode.toLowerCase());

                            if (groupIndex >= 0) {
                                // Found.
                                this.locationOptions[groupIndex].children.push({
                                    code: nodeId,
                                    name: txt,
                                    type: 'room'
                                });
                            }
                        });

                        roomsDone = true;
                        if (buildingDone) {
                            this.cacheLocationData();
                            this.loading = false;
                        }
                    });
                } else {
                    data.text().then((result) => {
                        // Something went wrong.
                        this.$bvToast.toast(result, {
                            title: 'Error',
                            autoHideDelay: 30000,
                            variant: 'danger'
                        });
                    });
                }
            }).catch((error) => {
                console.error(error);
            });
        }
    },

    beforeMount() {
        this.getAllCorridors();

        this.startingTime.selectedTime = moment().format('HH:mm');
    },

    mounted() {
        // Force the renderer to render more outside of the bounds of the viewport to avoid disappearing background map when panning.
        L.Renderer.prototype.options.padding = 5;

        this.getSelectBoxData();
        this.initialiseLeafletMap();
    }
});