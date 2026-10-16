import { Canvas, useThree } from '@react-three/fiber'
import { ContactShadows, OrbitControls } from '@react-three/drei'
import { useEffect } from 'react'
import * as THREE from 'three'
import type { AssetDefinition, GardenScene, Placement } from '../types'
import { cellToWorld } from '../lib/hex'
import { ADAPTIVE_HEX_SIZE, PATH_HEX_SIZE } from '../lib/imageScene'
import { AssetModel } from './AssetModel'

interface Props {
  scene: GardenScene
  selectedAssetId: string | null
  selectedPlacementId: string | null
  onCellClick(cellId: string): void
  onPlacementClick(placementId: string): void
  dropRequest: { assetId: string; clientX: number; clientY: number; nonce: number } | null
  onDropResolved(assetId: string, cellId: string): void
}

const surfaceColors: Record<string, string> = {
  moss: '#63745d', stone: '#8f8b7d', 'wet soil': '#645a4d', soil: '#75624d',
  'fallen leaves': '#8b704c', brick: '#917064', grass: '#78915e', gravel: '#aaa18d',
  sand: '#c2af82', 'stone path': '#a6a18e',
}

function getAsset(scene: GardenScene, placement: Placement): AssetDefinition {
  return scene.assets.find((asset) => asset.id === placement.assetId) ?? scene.assets[0]
}

function DropResolver({ scene, request, onResolve }: { scene: GardenScene; request: Props['dropRequest']; onResolve: Props['onDropResolved'] }) {
  const { camera, gl } = useThree()
  useEffect(() => {
    if (!request) return
    const rect = gl.domElement.getBoundingClientRect()
    const pointer = new THREE.Vector2(
      ((request.clientX - rect.left) / rect.width) * 2 - 1,
      -((request.clientY - rect.top) / rect.height) * 2 + 1,
    )
    const raycaster = new THREE.Raycaster()
    raycaster.setFromCamera(pointer, camera)
    const point = new THREE.Vector3()
    if (!raycaster.ray.intersectPlane(new THREE.Plane(new THREE.Vector3(0, 1, 0), -0.2), point)) return
    point.applyAxisAngle(new THREE.Vector3(0, 1, 0), 0.18)
    const candidates = scene.cells.filter((cell) => cell.type === 'environment' && cell.walkable)
    const closest = candidates.reduce<{ id: string; distance: number } | null>((best, cell) => {
      const [x, , z] = cellToWorld(cell)
      const distance = Math.hypot(point.x - x, point.z - z)
      return !best || distance < best.distance ? { id: cell.id, distance } : best
    }, null)
    if (closest && closest.distance < 2.1) onResolve(request.assetId, closest.id)
  }, [camera, gl, onResolve, request, scene])
  return null
}

function MicroPath({ scene }: { scene: GardenScene }) {
  return <group>
    {scene.pathHexes?.map((pathCell) => {
      const nearest = scene.cells.reduce<{ cell: GardenScene['cells'][number]; distance: number } | null>((best, cell) => {
        const [x, , z] = cellToWorld(cell)
        const distance = Math.hypot(x - pathCell.x, z - pathCell.z)
        return !best || distance < best.distance ? { cell, distance } : best
      }, null)?.cell
      const top = nearest ? 0.23 + nearest.height : 0.26
      return <mesh key={pathCell.id} position={[pathCell.x, top + 0.035, pathCell.z]} receiveShadow>
        <cylinderGeometry args={[PATH_HEX_SIZE * 1.012, PATH_HEX_SIZE * 1.012, 0.07, 6]} />
        <meshStandardMaterial color="#17221c" roughness={0.86} />
      </mesh>
    })}
  </group>
}

export function HexScene({ scene, selectedAssetId, selectedPlacementId, onCellClick, onPlacementClick, dropRequest, onDropResolved }: Props) {
  return (
    <Canvas shadows camera={{ position: [10.5, 11.8, 15.5], fov: 40 }} dpr={[1, 1.75]}>
      <color attach="background" args={['#d9d7ca']} />
      <fog attach="fog" args={['#d9d7ca', 13, 23]} />
      <ambientLight intensity={1.45} />
      <directionalLight castShadow position={[6, 10, 7]} intensity={2.2} shadow-mapSize={[1024, 1024]} />

      <group rotation={[0, -0.18, 0]}>
        {scene.cells.map((cell) => {
          const [x, , z] = cellToWorld(cell)
          const isTarget = selectedAssetId && cell.walkable
          const color = cell.type === 'boundary' ? '#52665b'
            : cell.sampledColor ?? surfaceColors[cell.surface] ?? scene.profile.palette[0]
          const baseSize = typeof cell.worldX === 'number' ? ADAPTIVE_HEX_SIZE * 1.008 : 1.04

          return (
            <group
              key={cell.id}
              position={[x, 0, z]}
              onClick={(event) => { event.stopPropagation(); onCellClick(cell.id) }}
              onPointerOver={(event) => { if (isTarget) document.body.style.cursor = 'copy'; event.stopPropagation() }}
              onPointerOut={() => { document.body.style.cursor = 'default' }}
            >
              <mesh position={[0, 0.055, 0]} receiveShadow>
                <cylinderGeometry args={[baseSize, baseSize, 0.11, 6]} />
                <meshStandardMaterial color="#c8c8bd" roughness={0.96} />
              </mesh>
              <mesh position={[0, 0.13 + cell.height / 2, 0]} receiveShadow>
                <cylinderGeometry args={[baseSize, baseSize, 0.15 + cell.height, 6]} />
                <meshStandardMaterial color={color} roughness={0.92} emissive={isTarget ? '#b9d683' : '#000000'} emissiveIntensity={isTarget ? 0.08 : 0} />
              </mesh>
            </group>
          )
        })}

        <MicroPath scene={scene} />

        {scene.placements.map((placement) => {
          const cell = scene.cells.find((item) => item.id === placement.cellId)
          if (!cell) return null
          const asset = getAsset(scene, placement)
          const [x, , z] = cellToWorld(cell)
          return (
            <group
              key={placement.id}
              position={[x, 0.22 + cell.height, z]}
              rotation={[0, placement.rotation, 0]}
              scale={placement.scale}
              onClick={(event) => { event.stopPropagation(); onPlacementClick(placement.id) }}
              onPointerOver={(event) => { document.body.style.cursor = 'pointer'; event.stopPropagation() }}
              onPointerOut={() => { document.body.style.cursor = 'default' }}
            >
              <AssetModel kind={asset.kind} color={scene.profile.palette[0]} selected={selectedPlacementId === placement.id} />
            </group>
          )
        })}
      </group>

      <DropResolver scene={scene} request={dropRequest} onResolve={onDropResolved} />

      <ContactShadows position={[0, -0.02, 0]} opacity={0.28} scale={18} blur={2.6} far={8} />
      <OrbitControls makeDefault enablePan={false} minDistance={8} maxDistance={18} minPolarAngle={0.55} maxPolarAngle={1.25} />
    </Canvas>
  )
}
