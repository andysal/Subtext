function flash(selector) {		$(selector)        	.css('opacity', 0)                .animate({opacity:1.0}, 800)                .animate({backgroundColor:'#ffffff'}, 350, function() {this.style.removeAttribute('filter');});}