module.exports = {
  // Source directory containing F# files
  sourceDirs: ["src"],
  
  // Output directory for generated JavaScript files
  outDir: "dist",
  
  // Target language (javascript for Node.js compatibility)
  lang: "javascript",
  
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
  
  // JavaScript configuration
  javascript: {
    // Target ES version
    target: "ES2020"
  }
};