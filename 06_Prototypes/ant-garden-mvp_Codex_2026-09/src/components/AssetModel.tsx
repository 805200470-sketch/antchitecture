import type { AssetKind } from '../types'

interface Props { kind: AssetKind; color: string; selected?: boolean }

export function AssetModel({ kind, color, selected }: Props) {
  const emissive = selected ? '#d3ff8c' : '#000000'

  if (kind === 'tree') {
    return (
      <group>
        <mesh position={[0, 0.42, 0]} castShadow><cylinderGeometry args={[0.12, 0.18, 0.85, 7]} /><meshStandardMaterial color="#725b43" /></mesh>
        <mesh position={[0, 1.08, 0]} castShadow><dodecahedronGeometry args={[0.58, 0]} /><meshStandardMaterial color={color} emissive={emissive} emissiveIntensity={0.2} /></mesh>
        <mesh position={[0.28, 1.15, -0.08]} castShadow><dodecahedronGeometry args={[0.35, 0]} /><meshStandardMaterial color={color} /></mesh>
      </group>
    )
  }

  if (kind === 'lamp') {
    return (
      <group>
        <mesh position={[0, 0.48, 0]} castShadow><cylinderGeometry args={[0.09, 0.14, 0.95, 8]} /><meshStandardMaterial color="#514c44" /></mesh>
        <mesh position={[0, 1.02, 0]} castShadow><boxGeometry args={[0.42, 0.42, 0.42]} /><meshStandardMaterial color={color} emissive={selected ? emissive : '#7c682e'} emissiveIntensity={0.35} /></mesh>
        <pointLight position={[0, 1.05, 0]} color="#ffd98b" intensity={1.2} distance={3} />
      </group>
    )
  }

  if (kind === 'flower') {
    return (
      <group>
        {[-0.28, 0, 0.28].map((x, index) => <group key={x} position={[x, 0, index === 1 ? 0.16 : -0.08]}>
          <mesh position={[0, 0.22, 0]}><cylinderGeometry args={[0.025, 0.035, 0.44, 6]} /><meshStandardMaterial color="#566c4d" /></mesh>
          <mesh position={[0, 0.48, 0]} castShadow><sphereGeometry args={[0.13, 8, 6]} /><meshStandardMaterial color={index === 1 ? '#e7b36f' : color} emissive={emissive} emissiveIntensity={0.15} /></mesh>
        </group>)}
      </group>
    )
  }

  if (kind === 'marker') {
    return (
      <group>
        <mesh position={[0, 0.35, 0]}><cylinderGeometry args={[0.04, 0.06, 0.7, 6]} /><meshStandardMaterial color="#5e4d3c" /></mesh>
        <mesh position={[0.16, 0.68, 0]} castShadow><boxGeometry args={[0.55, 0.25, 0.09]} /><meshStandardMaterial color={color} emissive={emissive} emissiveIntensity={0.2} /></mesh>
      </group>
    )
  }

  if (kind === 'pavilion') {
    return (
      <group>
        {[-0.34, 0.34].flatMap((x) => [-0.28, 0.28].map((z) => <mesh key={`${x}-${z}`} position={[x, 0.4, z]}><cylinderGeometry args={[0.045, 0.06, 0.8, 6]} /><meshStandardMaterial color="#735f48" /></mesh>))}
        <mesh position={[0, 0.86, 0]} castShadow rotation={[0, Math.PI / 4, 0]}><coneGeometry args={[0.82, 0.38, 4]} /><meshStandardMaterial color={color} emissive={emissive} emissiveIntensity={0.15} /></mesh>
      </group>
    )
  }

  if (kind === 'shrub') {
    return <group>
      {([[-0.25, 0, 0], [0.2, 0.04, 0.04], [0, 0.08, -0.22]] as Array<[number, number, number]>).map((position, index) => <mesh key={index} position={[position[0], 0.34 + position[1], position[2]]} castShadow>
        <dodecahedronGeometry args={[index === 1 ? 0.36 : 0.3, 0]} />
        <meshStandardMaterial color={index === 1 ? color : '#61765d'} emissive={emissive} emissiveIntensity={0.12} />
      </mesh>)}
    </group>
  }

  if (kind === 'grass') {
    return <group>
      {[-0.28, -0.14, 0, 0.14, 0.28].map((x, index) => <mesh key={x} position={[x, 0.22 + (index % 2) * 0.08, (index % 3 - 1) * 0.12]} rotation={[0, 0, (index - 2) * 0.08]} castShadow>
        <coneGeometry args={[0.055, 0.5 + (index % 2) * 0.13, 5]} />
        <meshStandardMaterial color={index % 2 ? '#829463' : color} />
      </mesh>)}
    </group>
  }

  if (kind === 'bench') {
    return <group>
      <mesh position={[0, 0.4, 0]} castShadow><boxGeometry args={[0.95, 0.13, 0.34]} /><meshStandardMaterial color="#80684e" /></mesh>
      <mesh position={[0, 0.68, 0.14]} castShadow rotation={[-0.12, 0, 0]}><boxGeometry args={[0.95, 0.42, 0.1]} /><meshStandardMaterial color={color} emissive={emissive} emissiveIntensity={0.12} /></mesh>
      {[-0.34, 0.34].map((x) => <mesh key={x} position={[x, 0.18, 0]}><boxGeometry args={[0.09, 0.38, 0.25]} /><meshStandardMaterial color="#51493e" /></mesh>)}
    </group>
  }

  if (kind === 'arch') {
    return <group>
      {[-0.38, 0.38].map((x) => <mesh key={x} position={[x, 0.58, 0]} castShadow><boxGeometry args={[0.22, 1.16, 0.28]} /><meshStandardMaterial color="#817b6d" /></mesh>)}
      <mesh position={[0, 1.12, 0]} castShadow><boxGeometry args={[0.98, 0.22, 0.32]} /><meshStandardMaterial color={color} emissive={emissive} emissiveIntensity={0.1} /></mesh>
    </group>
  }

  if (kind === 'mushroom') {
    return <group>
      {([[-0.22, 0.1], [0.08, -0.1], [0.3, 0.12]] as Array<[number, number]>).map(([x, z], index) => <group key={index} position={[x, 0, z]} scale={0.7 + index * 0.14}>
        <mesh position={[0, 0.22, 0]}><cylinderGeometry args={[0.055, 0.08, 0.42, 7]} /><meshStandardMaterial color="#ded4b5" /></mesh>
        <mesh position={[0, 0.46, 0]} castShadow scale={[1, 0.45, 1]}><sphereGeometry args={[0.22, 10, 7]} /><meshStandardMaterial color={index === 1 ? '#bd795d' : color} emissive={emissive} emissiveIntensity={0.12} /></mesh>
      </group>)}
    </group>
  }

  if (kind === 'water') {
    return <group>
      <mesh position={[0, 0.1, 0]} receiveShadow><cylinderGeometry args={[0.56, 0.62, 0.2, 12]} /><meshStandardMaterial color="#77796e" /></mesh>
      <mesh position={[0, 0.215, 0]}><cylinderGeometry args={[0.48, 0.48, 0.035, 12]} /><meshStandardMaterial color="#6f9999" metalness={0.08} roughness={0.25} emissive={emissive} emissiveIntensity={0.1} /></mesh>
    </group>
  }

  return (
    <mesh position={[0, 0.33, 0]} castShadow rotation={[0.2, 0.35, 0.08]}>
      <dodecahedronGeometry args={[0.46, 0]} />
      <meshStandardMaterial color={color} emissive={emissive} emissiveIntensity={0.2} roughness={0.92} />
    </mesh>
  )
}
