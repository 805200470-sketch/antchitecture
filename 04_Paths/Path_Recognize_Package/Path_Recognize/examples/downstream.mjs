/** Minimal downstream consumer. Coordinates stay in original-image pixels. */
export function acceptNetwork(network) {
  if (network.schemaVersion !== '2.5') throw new Error('Unsupported network schema');
  const nodes = new Map(network.nodes.map(node => [node.id, node]));
  if (nodes.size !== network.nodes.length) throw new Error('Duplicate node IDs');
  const adjacency = new Map([...nodes.keys()].map(id => [id, []]));
  const edgeIds = new Set();
  for (const node of nodes.values()) {
    if (![node.x, node.y].every(Number.isFinite)) throw new Error('Invalid node coordinates');
  }
  for (const edge of network.edges) {
    if (edgeIds.has(edge.id)) throw new Error('Duplicate edge IDs');
    edgeIds.add(edge.id);
    const source = nodes.get(edge.source), target = nodes.get(edge.target);
    if (!source || !target) throw new Error('Edge references missing node');
    if (edge.points.length < 2 || edge.points.some(p => p.length !== 2 || !p.every(Number.isFinite))) throw new Error('Invalid edge geometry');
    const first = edge.points[0], last = edge.points.at(-1);
    if (Math.hypot(first[0]-source.x, first[1]-source.y) > .01 || Math.hypot(last[0]-target.x, last[1]-target.y) > .01) throw new Error('Edge endpoint does not match node');
    if (edge.inferredSegments.length !== edge.points.length-1 || edge.inferredSegments.some(v => typeof v !== 'boolean')) throw new Error('Invalid segment provenance');
    adjacency.get(edge.source).push({nodeId: edge.target, edgeId: edge.id});
    adjacency.get(edge.target).push({nodeId: edge.source, edgeId: edge.id});
  }
  return {nodes, adjacency, network};
}
