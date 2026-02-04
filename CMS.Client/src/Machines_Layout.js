import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { OBJLoader } from 'three/addons/loaders/OBJLoader.js';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { MTLLoader } from 'three/addons/loaders/MTLLoader.js';
import { FontLoader } from 'three/addons/loaders/FontLoader.js';
import { TextGeometry } from 'three/addons/geometries/TextGeometry.js';
import { ColorManagement } from 'three';
import Stats from 'three/addons/libs/stats.module.js';

const existingMachines = new Map();
ColorManagement.enabled = true;

export async function initMachLayout(containerRef) {
  const scene = new THREE.Scene();
  scene.background = null;

  const clock = new THREE.Clock();

  // const stats = new Stats();
  // containerRef.appendChild(stats.dom);

  // Camera and Renderer
  const camera = new THREE.PerspectiveCamera(50, containerRef.clientWidth / containerRef.clientHeight, 0.1, 1000);
  camera.position.set(0, 40, 55);

  const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true, powerPreference: "high-performance" });
  renderer.setSize(containerRef.clientWidth, containerRef.clientHeight);
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFSoftShadowMap;
  containerRef.appendChild(renderer.domElement);

  // Lighting
  const ambientLight = new THREE.AmbientLight(0x404040, 1.2);
  scene.add(ambientLight);

  const directionalLight = new THREE.DirectionalLight(0xffffff, 2.5);
  directionalLight.position.set(-30, 40, 30);
  directionalLight.castShadow = true;
  scene.add(directionalLight);

  // Floor
  const floorGeometry = new THREE.PlaneGeometry(60, 60);
  const floorMaterial = new THREE.MeshBasicMaterial({ color: 0x808080 });
  const floor = new THREE.Mesh(floorGeometry, floorMaterial);
  floor.rotation.x = -Math.PI / 2;
  floor.receiveShadow = true;
  scene.add(floor);

  // Grid
  const grid = new THREE.GridHelper(60, 60, 0xcccccc, 0x444444);
  scene.add(grid);

  // Controls
  const controls = new OrbitControls(camera, renderer.domElement);
  controls.enableDamping = true;
  controls.dampingFactor = 0.1;
  controls.screenSpacePanning = false;
  controls.minDistance = 10;
  controls.maxDistance = 200;
  controls.maxPolarAngle = Math.PI / 2;

  // Add initial components
   await addWrapping(scene);
   await addRooms(scene);
   await addPackingMachine(scene);
   await addWalkingPath(scene);
   await addDoors(scene);
   // await addStaffModel(scene);

  // Animation Loop
  function animate() {
    const delta = clock.getDelta();
    machMotion(scene, delta);
    if (scene.userData.mixers) {
      scene.userData.mixers.forEach(mixer => mixer.update(delta));
    }
    if (scene.userData.billboards) {
      scene.userData.billboards.forEach(billboard => {
        const dx = camera.position.x - billboard.position.x;
        const dz = camera.position.z - billboard.position.z;
        billboard.rotation.y = Math.atan2(dx, dz);
        billboard.rotation.x = 0;
        billboard.rotation.z = 0;
      });
    }
    controls.update();
    renderer.render(scene, camera);
    // stats.update();
    requestAnimationFrame(animate);
  }
  animate();

  // Handle resizing
  window.addEventListener('resize', () => {
    camera.aspect = containerRef.clientWidth / containerRef.clientHeight;
    camera.updateProjectionMatrix();
    renderer.setSize(containerRef.clientWidth, containerRef.clientHeight);
  });

  return { scene, camera, renderer };
}

export async function updateMachLayout(scene, previousData) {
  const newMachineData = await fetchMachineData();
  if (JSON.stringify(newMachineData) !== JSON.stringify(previousData)) {
    const staticObjects = 100;
    while (scene.children.length > staticObjects) {
      scene.remove(scene.children[staticObjects]);
    }
    await populateMachines(scene, newMachineData);
    return newMachineData;
  }
  return previousData;
}

async function addRooms(scene) {
  const roomMaterial = new THREE.MeshBasicMaterial({ color: 0xaaaaaa, transparent: true, opacity: 0.7 });
  const wallMaterial = new THREE.MeshBasicMaterial({ color: 0xaaaaaa });
  const edgeMaterial = new THREE.LineBasicMaterial({ color: 0x000000 });

  // Define rooms
  const rooms = [
    { name: "CR 1", size: { x: 10, y: 6, z: 15 }, position: { x: -25, y: 3, z: -22.5 } },
    { name: "CR 2", size: { x: 5, y: 6, z: 10 }, position: { x: 27.5, y: 3, z: 25 } },
  ];

  for (const room of rooms) {
    const roomGeometry = new THREE.BoxGeometry(room.size.x, room.size.y, room.size.z);
    const roomMesh = new THREE.Mesh(roomGeometry, roomMaterial);

    roomMesh.position.set(room.position.x, room.position.y, room.position.z);

    scene.add(roomMesh);

    const edgesGeometry = new THREE.EdgesGeometry(roomGeometry);
    const edges = new THREE.LineSegments(edgesGeometry, edgeMaterial);
    edges.position.copy(roomMesh.position);

    scene.add(edges);

    const textPosition = {
      x: room.position.x,
      y: room.position.y + room.size.y / 2 + 1,
      z: room.position.z
    };
    const textGroup = await MachineText([room.name], textPosition);
    scene.add(textGroup);
  }

  const entry = [
    { positionShaft: { x: -25, y: 1, z: -20 }, positionHead: { x: -24, y: 1, z: -20 }, size: { x: 1, y: 1, z: 2 }, rotation: { x: 0, y: 0, z: -Math.PI / 2 }, color: 0x00ff00 },
    { positionShaft: { x: -24.5, y: 3, z: -20 }, positionHead: { x: -25.5, y: 3, z: -20 }, size: { x: 1, y: 1, z: 2 }, rotation: { x: 0, y: 0, z: Math.PI / 2 }, color: 0xff0000 },
    { positionShaft: { x: 27.5, y: 1, z: 25 }, positionHead: { x: 27.5, y: 1, z: 24 }, size: { x: 1, y: 1, z: 2 }, rotation: { x: -Math.PI / 2, y: 0, z: 0 }, color: 0x00ff00 },
    { positionShaft: { x: 27.5, y: 3, z: 24.5 }, positionHead: { x: 27.5, y: 3, z: 25.5 }, size: { x: 1, y: 1, z: 2 }, rotation: { x: Math.PI / 2, y: 0, z: 0 }, color: 0xff0000 },
    { positionShaft: { x: -29, y: 5, z: 0 }, positionHead: { x: -30, y: 5, z: 0 }, size: { x: 1, y: 1, z: 2 }, rotation: { x: 0, y: 0, z: Math.PI / 2 }, color: 0xff0000 },
  ];

  // Create arrows for each entry
  entry.forEach((entry) => {
    // Create arrow geometry
    const arrowShaftGeometry = new THREE.CylinderGeometry(0.1, 0.1, 2, 32);
    const arrowHeadGeometry = new THREE.ConeGeometry(0.5, 1, 32);

    const arrowMaterial = new THREE.MeshBasicMaterial({ color: entry.color });

    // Create shaft and head meshes
    const shaftMesh = new THREE.Mesh(arrowShaftGeometry, arrowMaterial);
    const headMesh = new THREE.Mesh(arrowHeadGeometry, arrowMaterial);

    // Position shaft
    shaftMesh.position.set(entry.positionShaft.x, entry.positionShaft.y + entry.size.y / 2 + 2, entry.positionShaft.z);
    shaftMesh.rotation.set(entry.rotation.x, entry.rotation.y, entry.rotation.z);

    // Position head at the end of the shaft
    headMesh.position.set(entry.positionHead.x, entry.positionHead.y + entry.size.y / 2 + 2, entry.positionHead.z);
    headMesh.rotation.set(entry.rotation.x, entry.rotation.y, entry.rotation.z);

    // Add shaft and head to the scene
    scene.add(shaftMesh);
    scene.add(headMesh);
  });

  // Walls
  const walls = [
    new THREE.Mesh(new THREE.PlaneGeometry(60, 6), wallMaterial), // Back Wall
    new THREE.Mesh(new THREE.PlaneGeometry(60, 6), wallMaterial), // Front Wall
    new THREE.Mesh(new THREE.PlaneGeometry(60, 6), wallMaterial), // Left Wall
    new THREE.Mesh(new THREE.PlaneGeometry(60, 6), wallMaterial),  // Right Wall
  ];

  walls[0].position.set(0, 3, -30); // Back Wall
  walls[1].position.set(0, 3, 30);  // Front Wall
  walls[1].rotation.y = Math.PI;
  walls[2].position.set(-30, 3, 0); // Left Wall
  walls[2].rotation.y = Math.PI / 2;
  walls[3].position.set(30, 3, 0);  // Right Wall
  walls[3].rotation.y = -Math.PI / 2;

  walls.forEach((wall) => {
    // Add wall mesh
    scene.add(wall);

    // Create edges for the wall
    const edgesGeometry = new THREE.EdgesGeometry(wall.geometry);
    const edges = new THREE.LineSegments(edgesGeometry, edgeMaterial);

    // Copy wall's position and rotation to the edges
    edges.position.copy(wall.position);
    edges.rotation.copy(wall.rotation);

    // Add edges to the scene
    scene.add(edges);
  });
}

async function fetchMachineData() {
  try {
    const response = await fetch('api/MachineLog/MachineMaster');
    const data = await response.json();
    return data;
  } catch (error) {
    console.error('Error fetching machine data:', error.message);
    return [ ];
  }
}

async function populateMachines(scene, machineData) {
  const machinePositions = {
    // First row
    A5: { x: -25, z: 20 },
    A10: { x: -20, z: 20 },
    A13: { x: -15, z: 20 },
    A14: { x: -10, z: 20 },
    A7: { x: -5, z: 20 },
    A12: { x: 0, z: 20 },
    A6: { x: 5, z: 20 },
    A15: { x: 10, z: 20 },
    A9: { x: 15, z: 20 },
    A8: { x: 20, z: 20 },

    // Second row
    A17: { x: -22, z: 12 },
    A19: { x: -17, z: 12 },
    A18: { x: -12, z: 12 },
    A21: { x: -7, z: 12 },
    A16: { x: -2, z: 12 },

    // Third row
    B3: { x: -25, z: 0 },
    SM10: { x: -21, z: 0 },
    SM5: { x: -17, z: 0 },
    Y1: { x: -13, z: 0 },
    KM26: { x: -6, z: -2 },
    KM18: { x: -2, z: -2 },
    A23: { x: 0, z: -2 },

    // Fourth row
    KM25: { x: -16, z: -15 },
    KM16: { x: -12, z: -15 },
    KM17: { x: -8, z: -15 },
  };

  const textPositions = {
    A5:   { x:   -23, y:    5, z:   22 },
    A10:  { x:   -19, y:    5, z:   22 },
    A13:  { x: -13.5, y:    5, z:   22 },
    A14:  { x:  -8.5, y:    5, z:   22 },
    A7:   { x:  -3.5, y:    5, z:   22 },
    A12:  { x:   1.5, y:    5, z:   22 },
    A6:   { x:     6, y:    5, z:   22 },
    A15:  { x:  11.5, y:    5, z:   22 },
    A9:   { x:    16, y:    5, z:   22 },
    A8:   { x:    21, y:    5, z:   22 },
                            
    A17:  { x:   -23, y:    5, z:    9 },
    A19:  { x:   -18, y:    5, z:    9 },
    A18:  { x:   -13, y:    5, z:    9 },
    A21:  { x:    -8, y:    5, z:    9 },
    A16:  { x:    -3, y:    5, z:    9 },
                            
    B3:   { x:   -23, y:    5, z:   -2 },
    SM10: { x:   -19, y:    5, z:   -2 },
    SM5:  { x:   -15, y:    5, z:   -2 },
    Y1:   { x: -11.5, y:    5, z:   -2 },
    KM18: { x:    -8, y:    5, z:   -2 },
    KM26: { x:    -4, y:    5, z:   -2 },
    A23:  { x:     1, y:    5, z:   -2 },

    KM25: { x:   -14, y:    5, z:  -17 },
    KM16: { x:   -10, y:    5, z:  -17 },
    KM17: { x:    -6, y:    5, z:  -17 },
  };

  for (const [name, position] of Object.entries(machinePositions)) {
    const color = machineData.find((machine) => machine.machine_name === name)?.color || 0xffffff;

    // Add the machine and get the reference
    const machine = await addMachine(scene, name, position.x, position.z, color, textPositions[name]);

    // Apply rotation for the first row
    if (position.z === 20 || name === 'A23') {
      machine.rotation.y = Math.PI;
    } else if (name === 'KM26' || name === 'KM18') {
      machine.rotation.y = Math.PI / 2;
    }
  }
}

async function addMachine(scene, name, x, z, color, textPosition) {
  color = color || 0xffffff;

  // Check if the machine already exists with the same color
  if (existingMachines.has(name)) {
    const existingMachine = existingMachines.get(name);
    if (existingMachine.color === color) {
      // Same machine and color, skip rendering
      return existingMachine.object;
    } else {
      // Remove the old machine with the different color
      scene.remove(existingMachine.object);
    }
  }

  const loader = new GLTFLoader();

  const machineSize = {
    A: { x: 3.055, y: 4.000, z: 4.000 },
    SM: { x: 2.000, y: 3.000, z: 3.000 },
    B: { x: 2.055, y: 3.000, z: 4.000 },
    KM: { x: 4.000, y: 3.500, z: 3.000 },
    Y: { x: 2.055, y: 3.000, z: 4.000 }
  };

  // Determine machine type from name prefix
  const type = name.match(/[A-Z]+/)[0];
  const size = machineSize[type];

  let machine;

  if (type === 'A' && color === '#00ff00') {
    // Load AOKI model for type A
    try {
      const gltf = await loader.loadAsync('./3d_model/ISBM_Run.glb');
      machine = gltf.scene;

      const bbox = new THREE.Box3().setFromObject(machine);
      const modelSize = bbox.getSize(new THREE.Vector3());

      const scaleX = size.x / modelSize.x;
      const scaleY = size.y / modelSize.y;
      const scaleZ = size.z / modelSize.z;
      machine.scale.set(scaleX, scaleY, scaleZ);

      const material = new THREE.MeshStandardMaterial({
        color,
        roughness: 0.5,
        metalness: 0.1
      });

      const aokiRun = machine.getObjectByName('AOKI_Closeobj');
      if (aokiRun) {
        aokiRun.traverse((child) => {
          if (child.isMesh) {
            child.material = material;
            child.castShadow = true;
            child.receiveShadow = true;
          }
        });
      }

      // Position the machine
      machine.position.set(x, 0, z);
    } catch (error) {
      console.error('Error loading AOKI model:', error);
    }
  } else if (type === 'A' && color !== '#00ff00') {
    // Load AOKI model for type A
    try {
      const gltf = await loader.loadAsync('./3d_model/AOKI_Stop.glb');
      machine = gltf.scene;

      const bbox = new THREE.Box3().setFromObject(machine);
      const modelSize = bbox.getSize(new THREE.Vector3());

      const scaleX = size.x / modelSize.x + 0.2;
      const scaleY = size.y / modelSize.y;
      const scaleZ = size.z / modelSize.z + 0.04;
      machine.scale.set(scaleX, scaleY, scaleZ);

      const material = new THREE.MeshStandardMaterial({
        color,
        roughness: 0.5,
        metalness: 0.1
      });

      machine.traverse((child) => {
        if (child.isMesh) {
          child.material = material; 
          child.castShadow = true;
          child.receiveShadow = true;
        }
      });

      machine.position.set(x, size.y - 0.3, z);

    } catch (error) {
      console.error('Error loading AOKI model:', error);
    }
  } else if (type === 'B' || type === 'SM' || type === 'Y') {
    // Load AOKI model for Injection Machine
    try {
      const loader = new GLTFLoader();
      const gltf = await loader.loadAsync('./3d_model/Injection_Machine.glb');
      machine = gltf.scene;

      // Scale the model to match the desired size
      const bbox = new THREE.Box3().setFromObject(machine);
      const modelSize = bbox.getSize(new THREE.Vector3());

      const scaleX = size.x / modelSize.x;
      const scaleY = size.y / modelSize.y;
      const scaleZ = size.z / modelSize.z;

      machine.scale.set(scaleX, scaleY, scaleZ);
      machine.position.set(x, size.y / 2, z);
    } catch (error) {
      console.error('Error loading AOKI model:', error);
    }
  } else if (type === 'KM') {
    // Load AOKI model for type KM
    try {
      const gltf = await loader.loadAsync('./3d_model/KaiMei.glb');
      machine = gltf.scene;

      const bbox = new THREE.Box3().setFromObject(machine);
      const modelSize = bbox.getSize(new THREE.Vector3());

      const scaleX = size.x / modelSize.x;
      const scaleY = size.y / modelSize.y;
      const scaleZ = size.z / modelSize.z;
      machine.scale.set(scaleX, scaleY, scaleZ);

      const material = new THREE.MeshStandardMaterial({
        color,
        roughness: 0.5,
        metalness: 0.1
      });

      machine.traverse((child) => {
        if (child.isMesh) {
          child.material = material;
          child.castShadow = true;
          child.receiveShadow = true;
        }
      });

      // Position the machine
      machine.position.set(x, 0, z);
      machine.rotation.y = - Math.PI / 2;

    } catch (error) {
      console.error('Error loading KAIMEI model:', error);
    }
  }

  scene.add(machine);

  // Track the machine in the map
  existingMachines.set(name, { object: machine, color });

  const textGroup = await MachineText([name], {
    x: textPosition.x,
    y: textPosition.y,
    z: textPosition.z
  });

  scene.add(textGroup);

  // Add billboard tracking
  scene.userData.billboards = scene.userData.billboards || [];
  scene.userData.billboards.push(textGroup);
  return machine;
}

async function addPackingMachine(scene) {
  const loader = new GLTFLoader();

  try {
    const gltf = await loader.loadAsync('./3d_model/Robot_Arm.glb'); // Ensure model is in public folder
    const object = gltf.scene;
    const animations = gltf.animations;

    // Set model transform
    object.position.set(2, 1, 9);
    object.scale.set(0.55, 0.55, 0.55);
    object.rotation.y = Math.PI / 2;
    scene.add(object);

    // Setup animation mixer
    const mixer = new THREE.AnimationMixer(object);
    if (animations && animations.length > 0) {
      const action = mixer.clipAction(animations[0]);
      action.play();
    }

    // Add mixer to global update loop
    scene.userData.mixers = scene.userData.mixers || [];
    scene.userData.mixers.push(mixer);

    const textGroup = await MachineText(['AUTO\nPACKING'], {
      x: object.position.x,
      y: object.position.y + 5,
      z: object.position.z
    });

    scene.add(textGroup);

    // Add billboard tracking
    scene.userData.billboards = scene.userData.billboards || [];
    scene.userData.billboards.push(textGroup);

    // Add white cube platform
    const cubeGeometry = new THREE.BoxGeometry(2, 1, 2);
    const cubeMaterial = new THREE.MeshBasicMaterial({ color: 0xffffff });
    const cube = new THREE.Mesh(cubeGeometry, cubeMaterial);
    cube.position.set(object.position.x, 0.5, object.position.z);
    scene.add(cube);

    const edges = new THREE.LineSegments(
      new THREE.EdgesGeometry(cubeGeometry),
      new THREE.LineBasicMaterial({ color: 0x000000 })
    );
    edges.position.copy(cube.position);
    scene.add(edges);
  } catch (error) {
    console.error('An error occurred while loading the GLB model:', error);
  }
}

async function addWrapping(scene) {
  const loader = new GLTFLoader();

  const gltf = await loader.loadAsync('./3d_model/Wrapping_Machine.glb');
  const object = gltf.scene;

  const positions = [
    { x: 15, y: 2.6, z: 5 },
    { x: 20, y: 2.6, z: 5 }
  ];

  positions.forEach(pos => {
    const object = gltf.scene.clone();
    object.position.set(pos.x, pos.y, pos.z);
    object.scale.set(3, 3, 3);
    scene.add(object);
  });
}

async function machMotion(scene, delta) {
  if (scene.userData.mixers) {
    scene.userData.mixers.forEach((mixer) => mixer.update(delta));
  }
}

async function addWalkingPath(scene) {
  const tapeHeight = 0.02;
  const tapeWidth = 0.1;
  const tapeColor = 0xFFFF00;

  // Array of path definitions
  const paths = [
    { position: { x: 0, y: tapeHeight, z: 17 }, rotation: { x: -Math.PI / 2, y: 0, z: 0 }, pathWidth: 60 },
    { position: { x: -15, y: tapeHeight, z: 13 }, rotation: { x: -Math.PI / 2, y: 0, z: 0 }, pathWidth: 40 },
    { position: { x: 20, y: tapeHeight, z: 13 }, rotation: { x: -Math.PI / 2, y: 0, z: 0 }, pathWidth: 20 },
    { position: { x: -15, y: tapeHeight, z: 7 }, rotation: { x: -Math.PI / 2, y: 0, z: 0 }, pathWidth: 40 },
    { position: { x: -15, y: tapeHeight, z: 3 }, rotation: { x: -Math.PI / 2, y: 0, z: 0 }, pathWidth: 40 },
    { position: { x: -15, y: tapeHeight, z: -3 }, rotation: { x: -Math.PI / 2, y: 0, z: 0 }, pathWidth: 40 },
    { position: { x: 20, y: tapeHeight, z: -3 }, rotation: { x: -Math.PI / 2, y: 0, z: 0 }, pathWidth: 20 },
    { position: { x: 0, y: tapeHeight, z: -7 }, rotation: { x: -Math.PI / 2, y: 0, z: 0 }, pathWidth: 60 },
    { position: { x: 5, y: tapeHeight, z: 0 }, rotation: { x: -Math.PI / 2, y: 0, z: -Math.PI / 2 }, pathWidth: 6 },
    { position: { x: 5, y: tapeHeight, z: 10 }, rotation: { x: -Math.PI / 2, y: 0, z: -Math.PI / 2 }, pathWidth: 6 },
    { position: { x: 10, y: tapeHeight, z: 5 }, rotation: { x: -Math.PI / 2, y: 0, z: -Math.PI / 2 }, pathWidth: 16 },
  ];

  // Function to create a tape line with custom properties
  const createTapeLine = (position, rotation, pathWidth) => {
    const tapeGeometry = new THREE.PlaneGeometry(pathWidth, tapeWidth);
    const tapeMaterial = new THREE.MeshBasicMaterial({
      color: tapeColor,
      side: THREE.DoubleSide
    });
    const tape = new THREE.Mesh(tapeGeometry, tapeMaterial);
    tape.rotation.set(rotation.x, rotation.y, rotation.z);
    tape.position.set(position.x, position.y, position.z);
    scene.add(tape);
  };

  // Create tape lines for the walking paths from the paths array
  paths.forEach(path => {
    createTapeLine(path.position, path.rotation, path.pathWidth);
  });
}


async function addDoors(scene) {
  const doorGeometry = new THREE.BoxGeometry(1, 2, 1);
  const doorMaterial = new THREE.MeshBasicMaterial({ color: 0x654321 });

  const leftDoor = new THREE.Mesh(doorGeometry, doorMaterial);
  leftDoor.position.set(-30, 1, 0);
  leftDoor.rotation.y = Math.PI / 2;
  scene.add(leftDoor);
}

async function addStaffModel(scene) {
  const loader = new GLTFLoader();
  const gltf = await loader.loadAsync('./3d_model/Staff_Model.glb');

  const staffConfigs = [
    { position: [0, 0, 0], rotation: [0, 0, 0]},
    { position: [5, 0, 0], rotation: [0, Math.PI / 4, 0]},
    { position: [-5, 0, 0], rotation: [0, -Math.PI / 4, 0]},
    { position: [0, 0, 5], rotation: [0, Math.PI / 2, 0]},
    { position: [0, 0, -5], rotation: [0, -Math.PI / 2, 0]}
  ];

  staffConfigs.forEach(config => {
    const staffClone = gltf.scene.clone();
    staffClone.position.set(...config.position);
    staffClone.rotation.set(...config.rotation);
    scene.add(staffClone);
  });
}

async function MachineText(lines, position) {
  const fontLoader = new FontLoader();
  const font = await new Promise((resolve, reject) => {
    fontLoader.load('./fonts/helvetiker_regular.typeface.json', resolve, undefined, reject);
  });

  const textGroup = new THREE.Group();

  const textMaterial = new THREE.MeshBasicMaterial({
    color: 0x000000,
    transparent: true,
    opacity: 1
  });

  lines.forEach((line, index) => {
    const textGeometry = new TextGeometry(line, {
      font,
      size: 0.8,
      depth: 0.02,
      curveSegments: 8,
      bevelEnabled: false
    });

    textGeometry.computeBoundingBox();
    const textWidth = textGeometry.boundingBox.max.x - textGeometry.boundingBox.min.x;
    const centerX = -textWidth / 2;

    // Main text mesh
    const textMesh = new THREE.Mesh(textGeometry, textMaterial);
    textMesh.position.set(centerX, -index * 1, 0.01);

    // Create outline using shapes
    const shapes = font.generateShapes(line, 0.8);
    const outlineGroup = new THREE.Group();

    shapes.forEach(shape => {
      const points = shape.getPoints();
      const outlineGeometry = new THREE.BufferGeometry().setFromPoints(points);
      const outlineMesh = new THREE.Line(outlineGeometry, new THREE.LineBasicMaterial({
        color: 0xffffff,
        linewidth: 3,
        transparent: true,
        opacity: 0.9
      }));
      outlineGroup.add(outlineMesh);
    });

    outlineGroup.position.set(centerX, -index * 1, 0);

    textGroup.add(outlineGroup);
    textGroup.add(textMesh);
  });

  textGroup.position.set(position.x, position.y, position.z);
  return textGroup;
}
