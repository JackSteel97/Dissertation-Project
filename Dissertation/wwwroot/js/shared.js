const NodeType = Object.freeze({
    MaleToilet: 0,
    GenderNeutralToilet: 1,
    DisabledToilet: 2,
    Parking: 3,
    ShowerRoom: 4,
    Unrouteable: 5,
    Corridor: 6,
    FemaleToilet: 7,
    UnisexToilet: 8,
    Stairs: 9,
    Lift: 10,
    Room: 11,
    BabyChanging: 12,
    Unknown: 255
});

// Returns a bearing in the range 0 - 360 degrees (inclusive - exclusive), where 0 is due north, and 180 is due south
function calculateBearingFromLatLonPoints(lat1, lon1, lat2, lon2) {
    // Convert to radians
    lat1 = deg2rad(lat1);
    lat2 = deg2rad(lat2);
    lon1 = deg2rad(lon1);
    lon2 = deg2rad(lon2);
    // Get delta(Longitude)
    let dLon = lon2 - lon1;
    // Equation source: http://www.movable-type.co.uk/scripts/latlong.html
    let y = Math.cos(lat2) * Math.sin(dLon);
    let x = Math.cos(lat1) * Math.sin(lat2) - Math.sin(lat1) * Math.cos(lat2) * Math.cos(dLon);
    let b = radToDeg(Math.atan2(y, x));
    // Normalise result
    let bearing = (b + 360) % 360;
    return bearing;
}

// Function to calculate a distance in meters from two lat/long coordinates
function getDistanceFromLatLonInMeters(lat1, lon1, lat2, lon2) {
    if (!lat1 || !lat2 || !lon1 || !lon2) {
        return 0;
    }
    // Radius of the earth in km
    const R = 6371;
    let dLat = deg2rad(lat2 - lat1);
    let dLon = deg2rad(lon2 - lon1);

    // Equation source: http://www.movable-type.co.uk/scripts/latlong.html
    let a = Math.sin(dLat / 2) * Math.sin(dLat / 2) + Math.cos(deg2rad(lat1)) * Math.cos(deg2rad(lat2)) * Math.sin(dLon / 2) * Math.sin(dLon / 2);

    let c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
    // Distance in km
    let d = R * c;
    // Distance in meters
    return d * 1000;
}

function CapitaliseWords(words) {
    let capsName = '';
    let splt = words.split(' ');
    for (let i = 0; i < splt.length; i++) {
        capsName += splt[i][0].toUpperCase() + splt[i].slice(1);
        if (i < splt.length - 1) {
            capsName += ' ';
        }
    }
    return capsName;
}

// Function to convert radians to degrees
function radToDeg(rad) {
    return rad * 180 / Math.PI;
}

// Function to convert degrees to radians
function deg2rad(deg) {
    return deg * (Math.PI / 180);
}