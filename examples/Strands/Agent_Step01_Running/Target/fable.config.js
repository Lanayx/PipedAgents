module.exports = {
  // Source directory containing F# files
  sourceDirs: ["src"],
  
  // Output directory for generated TypeScript files
  outDir: "dist",
  
  // Target language (typescript for Node.js compatibility)
  lang: "typescript",
  
  // Fable compiler options
  fableOptions: {
    // Define constants for conditional compilation
    define: ["FABLE_COMPILER"],
    
    // Optimize for production builds
    optimize: false,
    
    // Generate source maps for debugging
    sourceMaps: true
  },
  
  // Module system configuration
  moduleSystem: "commonjs",
  
  // TypeScript configuration
  typescript: {
    // Target ES version
    target: "ES2020",
    
    // Module resolution
    moduleResolution: "node"
  }
};