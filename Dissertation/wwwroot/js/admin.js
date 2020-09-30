var adminapp = new Vue({
    el: '#admin-app',
    data: {
        floorFile: [],
        backgroundFloorFile: null,
        uploading: false,
        newBuildingCode: '',
        newBuildingName: '',
        deleteBuilding: {
            selected: null,
            options: []
        },
        generatingTimetable: false
    },

    beforeMount() {
        this.getAllBuildings();
    },

    methods: {
        generateTimetableData() {
            this.generatingTimetable = true;
            fetch(`${window.location.origin}/admin/timetable/generate`, {
                method: 'post',
                headers: {
                    "Content-type": "application/json; charset=UTF-8"
                }
            }).then((data) => {
                if (data.ok) {
                    this.$bvToast.toast(`Successfully generated timetable data`, {
                        title: 'Success',
                        autoHideDelay: 10000,
                        variant: 'success'
                    });
                } else {
                    // Something went wrong.
                    this.$bvToast.toast('Failed to generate timetable data', {
                        title: 'Error',
                        autoHideDelay: 30000,
                        variant: 'danger'
                    });
                }
            }).catch((error) => {
                console.error(error);
            }).finally(() => {
                this.generatingTimetable = false;
            });
        },

        addBuilding() {
            if (this.newBuildingCode.length > 0 && this.newBuildingName.length > 0) {
                this.uploading = true;
                let bodyData = {
                    BuildingCode: this.newBuildingCode,
                    BuildingName: this.newBuildingName
                };

                fetch(`${window.location.origin}/admin/building`, {
                    method: 'post',
                    headers: {
                        "Content-type": "application/json; charset=UTF-8"
                    },
                    body: JSON.stringify(bodyData)
                }).then((data) => {
                    if (data.ok) {
                        this.$bvToast.toast('Building Created', {
                            title: 'Success',
                            autoHideDelay: 10000,
                            variant: 'success'
                        });
                        this.getAllBuildings();
                    } else {
                        // Something went wrong.
                        this.$bvToast.toast('Failed to create building', {
                            title: 'Error',
                            autoHideDelay: 30000,
                            variant: 'danger'
                        });
                    }
                }).catch((error) => {
                    console.error(error);
                }).finally(() => {
                    this.uploading = false;
                });
            }
        },

        getAllBuildings() {
            fetch(`${window.location.origin}/campusdata/buildings`, {
                method: 'get',
                headers: {
                    "Content-type": "application/json; charset=UTF-8"
                }
            }).then((data) => {
                if (data.ok) {
                    data.json().then((result) => {
                        this.deleteBuilding.options = result;
                        this.deleteBuilding.selected = null;
                    });
                } else {
                    // Something went wrong.
                    this.$bvToast.toast('Failed to get buildings', {
                        title: 'Error',
                        autoHideDelay: 30000,
                        variant: 'danger'
                    });
                }
            }).catch((error) => {
                console.error(error);
            });
        },

        deleteBuildingClicked() {
            if (this.deleteBuilding.selected) {
                fetch(`${window.location.origin}/admin/building/${this.deleteBuilding.selected.buildingCode}`, {
                    method: 'delete',
                    headers: {
                        "Content-type": "application/json; charset=UTF-8"
                    }
                }).then((data) => {
                    if (data.ok) {
                        this.$bvToast.toast(`Successfully deleted ${this.deleteBuilding.selected.buildingName}`, {
                            title: 'Success',
                            autoHideDelay: 10000,
                            variant: 'success'
                        });
                        this.getAllBuildings();
                    } else {
                        // Something went wrong.
                        this.$bvToast.toast('Failed to delete building', {
                            title: 'Error',
                            autoHideDelay: 30000,
                            variant: 'danger'
                        });
                    }
                }).catch((error) => {
                    console.error(error);
                });
            }
        },

        uploadButtonClicked() {
            if (this.floorFile && this.floorFile.length > 0) {
                for (let i = 0; i < this.floorFile.length; i++) {
                    let reader = new FileReader();
                    reader.onload = fileData => {
                        this.uploading = true;

                        // Send to JOSM 2 leaflet conversion.
                        // Send to node extractor.
                        Promise.all([
                            this.josm2Leaflet(fileData.target.result),
                            this.extractNodes(fileData.target.result)
                        ]).then(() => this.uploading = false);
                    };
                    reader.onerror = error => {
                        this.$bvToast.toast(`Failed to read file: ${error}`, {
                            title: 'Error',
                            autoHideDelay: 30000,
                            variant: 'danger'
                        });
                    };

                    reader.readAsText(this.floorFile[i]);
                }
            }
        },

        uploadBackgroundButtonClicked() {
            if (this.backgroundFloorFile) {
                let reader = new FileReader();
                reader.onload = fileData => {
                    this.uploading = true;

                    // Send to JOSM 2 leaflet conversion.
                    // Send to node extractor.
                    Promise.all([
                        this.backgroundJosm2Leaflet(fileData.target.result),
                        this.extractNodes(fileData.target.result)
                    ]).then(this.fixCorridorAreas)
                        .then(() => this.uploading = false);
                };
                reader.onerror = error => {
                    this.$bvToast.toast(`Failed to read file: ${error}`, {
                        title: 'Error',
                        autoHideDelay: 30000,
                        variant: 'danger'
                    });
                };

                reader.readAsText(this.backgroundFloorFile);
            }
        },

        importTimetableClicked() {
            this.uploading = true;

            fetch(`${window.location.origin}/admin/timetable/importFromFile`, {
                method: 'post',
                headers: {
                    "Content-type": "application/json; charset=UTF-8"
                }
            }).then((data) => {
                if (data.ok) {
                    data.text().then(result => {
                        this.$bvToast.toast(result, {
                            title: 'Timetable Import Success',
                            autoHideDelay: 10000,
                            variant: 'success'
                        });
                    });
                } else {
                    this.$bvToast.toast("Failed", {
                        title: 'Timetable Import Error',
                        autoHideDelay: 30000,
                        variant: 'danger'
                    });
                }
            }).catch((error) => {
                console.error(error);
            }).finally(() => {
                this.uploading = false;
            });
        },

        // This should be done only after uploading the background map file, this is to prevent it being called multiple times unnecessarily.
        fixCorridorAreas() {
            // Call the endpoint to fix any missing areas at the transition between buildings/floors.
            return new Promise((resolve, reject) => {
                fetch(`${window.location.origin}/admin/nodes/fixarea`, {
                    method: 'put',
                    headers: {
                        "Content-type": "application/json; charset=UTF-8"
                    }
                }).then((data) => {
                    if (!data.ok) {
                        // Something went wrong.
                        data.text().then(result => {
                            this.$bvToast.toast(result, {
                                title: 'Fix Areas Error',
                                autoHideDelay: 30000,
                                variant: 'danger'
                            });
                        });
                    }
                    resolve();
                }).catch((error) => {
                    console.error(error);
                    reject();
                });
            });
        },

        backgroundJosm2Leaflet(fileContents) {
            return new Promise((resolve, reject) => {
                fetch(`${window.location.origin}/admin/josm/background/josmtoleaflet/`, {
                    method: 'post',
                    headers: {
                        "Content-type": "application/json; charset=UTF-8"
                    },
                    body: fileContents
                }).then((data) => {
                    if (data.ok) {
                        data.text().then(result => {
                            this.$bvToast.toast(result, {
                                title: 'JOSM2Leaflet Success',
                                autoHideDelay: 10000,
                                variant: 'success'
                            });
                        });
                    } else {
                        data.json().then(result => {
                            // Something went wrong.
                            for (let i = 0; i < result.length; i++) {
                                this.$bvToast.toast(result[i], {
                                    title: 'JOSM2Leaflet Error',
                                    autoHideDelay: 30000,
                                    variant: 'danger'
                                });
                            }
                        });
                    }
                    resolve();
                }).catch((error) => {
                    console.error(error);
                    reject();
                });
            });
        },

        josm2Leaflet(fileContents) {
            return new Promise((resolve, reject) => {
                fetch(`${window.location.origin}/admin/josm/josmtoleaflet`, {
                    method: 'post',
                    headers: {
                        "Content-type": "application/json; charset=UTF-8"
                    },
                    body: fileContents
                }).then((data) => {
                    if (data.ok) {
                        data.text().then(result => {
                            this.$bvToast.toast(result, {
                                title: 'JOSM2Leaflet Success',
                                autoHideDelay: 10000,
                                variant: 'success'
                            });
                        });
                    } else {
                        data.json().then(result => {
                            // Something went wrong.
                            for (let i = 0; i < result.length; i++) {
                                this.$bvToast.toast(result[i], {
                                    title: 'JOSM2Leaflet Error',
                                    autoHideDelay: 30000,
                                    variant: 'danger'
                                });
                            }
                        });
                    }
                    resolve();
                }).catch((error) => {
                    console.error(error);
                    reject();
                });
            });
        },

        extractNodes(fileContents) {
            return new Promise((resolve, reject) => {
                fetch(`${window.location.origin}/admin/nodes/extractnodes`, {
                    method: 'post',
                    headers: {
                        "Content-type": "application/json; charset=UTF-8"
                    },
                    body: fileContents
                }).then((data) => {
                    if (data.ok) {
                        data.json().then(result => {
                            this.$bvToast.toast(result.processingTime, {
                                title: 'Node Extractor Success',
                                autoHideDelay: 10000,
                                variant: 'success'
                            });

                            for (let i = 0; i < result.warnings.length; i++) {
                                this.$bvToast.toast(result.warnings[i], {
                                    title: 'Node Extractor Warning',
                                    autoHideDelay: 30000,
                                    variant: 'warning'
                                });
                            }
                        });
                    } else {
                        data.json().then(result => {
                            // Something went wrong.
                            for (let i = 0; i < result.nodeExtractorErrors.length; i++) {
                                this.$bvToast.toast(result.nodeExtractorErrors[i], {
                                    title: 'Node Extractor Error',
                                    autoHideDelay: 30000,
                                    variant: 'danger'
                                });
                            }

                            for (let i = 0; i < result.nodeExtractorWarnings.length; i++) {
                                this.$bvToast.toast(result.nodeExtractorWarnings[i], {
                                    title: 'Node Extractor Warning',
                                    autoHideDelay: 30000,
                                    variant: 'warning'
                                });
                            }
                        });
                    }
                    resolve();
                }).catch((error) => {
                    console.error(error);
                    reject();
                });
            });
        }
    }
});